using System.Buffers.Binary;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Assets;

public sealed record MeshPart(string Name, VertexPositionColor[] Vertices);

// Static, untextured glTF 2.0 subset. Transforms and inverse-transpose normals
// are baked on import. Unsupported features fail explicitly rather than disappear.
public sealed class GlbScene
{
    public List<MeshPart> Parts { get; } = [];
    public Dictionary<string, Matrix> NodeTransforms { get; } = new(StringComparer.Ordinal);
    public Vector3 Min { get; private set; } = new(float.MaxValue);
    public Vector3 Max { get; private set; } = new(float.MinValue);

    public static GlbScene Load(string path)
    {
        byte[] file = File.ReadAllBytes(path);
        if (file.Length < 20 || U32(file, 0) != 0x46546C67 || U32(file, 4) != 2 || U32(file, 8) != file.Length)
            throw new InvalidDataException("Expected a complete binary glTF 2.0 file.");
        byte[]? json = null, bin = null;
        for (int offset = 12; offset < file.Length;)
        {
            int length = checked((int)U32(file, offset));
            uint type = U32(file, offset + 4);
            var chunk = file.AsSpan(offset + 8, length).ToArray();
            if (type == 0x4E4F534A) json = chunk;
            if (type == 0x004E4942) bin = chunk;
            offset = checked(offset + 8 + length);
        }
        using var document = JsonDocument.Parse(json ?? throw new InvalidDataException("Missing JSON chunk."));
        var root = document.RootElement;
        var data = bin ?? throw new InvalidDataException("Missing embedded mesh buffer.");
        if (root.TryGetProperty("extensionsRequired", out var extensions) && extensions.GetArrayLength() > 0)
            throw new NotSupportedException($"Required glTF extensions: {extensions}");
        var result = new GlbScene();
        var nodes = root.GetProperty("nodes");
        var scene = root.GetProperty("scenes")[Int(root, "scene", 0)];
        foreach (var node in scene.GetProperty("nodes").EnumerateArray()) Visit(node.GetInt32(), Matrix.Identity, new HashSet<int>());
        if (result.Parts.Count == 0) throw new InvalidDataException("Scene contains no triangles.");
        return result;

        void Visit(int index, Matrix parent, HashSet<int> ancestors)
        {
            if (!ancestors.Add(index)) throw new InvalidDataException("Cyclic node hierarchy.");
            var node = nodes[index];
            if (node.TryGetProperty("skin", out _)) throw new NotSupportedException("Skinned assets require animation support; export an unrigged base model.");
            Matrix local;
            if (node.TryGetProperty("matrix", out var matrix))
            {
                var m = matrix.EnumerateArray().Select(v => v.GetSingle()).ToArray();
                local = new Matrix(m[0],m[1],m[2],m[3],m[4],m[5],m[6],m[7],m[8],m[9],m[10],m[11],m[12],m[13],m[14],m[15]);
            }
            else
            {
                var s = Vec(node, "scale", Vector3.One);
                var t = Vec(node, "translation", Vector3.Zero);
                var q = Quaternion.Identity;
                if (node.TryGetProperty("rotation", out var r)) q = new Quaternion(r[0].GetSingle(), r[1].GetSingle(), r[2].GetSingle(), r[3].GetSingle());
                local = Matrix.CreateScale(s) * Matrix.CreateFromQuaternion(q) * Matrix.CreateTranslation(t);
            }
            var world = local * parent; // glTF column-major to XNA row-vector convention.
            if (node.TryGetProperty("name", out var nodeName)) result.NodeTransforms[nodeName.GetString()!] = world;
            var normalMatrix = Matrix.Transpose(Matrix.Invert(world));
            if (node.TryGetProperty("mesh", out var meshIndex))
                foreach (var primitive in root.GetProperty("meshes")[meshIndex.GetInt32()].GetProperty("primitives").EnumerateArray())
                {
                    if (Int(primitive, "mode", 4) != 4 || primitive.TryGetProperty("targets", out _))
                        throw new NotSupportedException("Only static triangle primitives are supported.");
                    var attributes = primitive.GetProperty("attributes");
                    var positions = Read(attributes.GetProperty("POSITION").GetInt32());
                    var normals = attributes.TryGetProperty("NORMAL", out var normal) ? Read(normal.GetInt32()) : null;
                    var colors = attributes.TryGetProperty("COLOR_0", out var color) ? Read(color.GetInt32()) : null;
                    var indices = primitive.TryGetProperty("indices", out var indicesId) ? Read(indicesId.GetInt32()).Select(v => checked((int)v[0])).ToArray() : Enumerable.Range(0, positions.Length).ToArray();
                    if (indices.Length % 3 != 0) throw new InvalidDataException("Incomplete triangle.");
                    var tint = Vector4.One;
                    if (primitive.TryGetProperty("material", out var materialIndex))
                    {
                        var material = root.GetProperty("materials")[materialIndex.GetInt32()];
                        if (material.TryGetProperty("pbrMetallicRoughness", out var pbr))
                        {
                            if (pbr.TryGetProperty("baseColorTexture", out _)) throw new NotSupportedException("This flat-color viewer does not support textures.");
                            if (pbr.TryGetProperty("baseColorFactor", out var c)) tint = new(c[0].GetSingle(),c[1].GetSingle(),c[2].GetSingle(),c[3].GetSingle());
                        }
                    }
                    var vertices = new VertexPositionColor[indices.Length];
                    for (int triangle = 0; triangle < indices.Length; triangle += 3)
                    {
                        var a = Position(indices[triangle]); var b = Position(indices[triangle+1]); var c = Position(indices[triangle+2]);
                        var face = SafeNormal(Vector3.Cross(b-a,c-a));
                        for (int corner = 0; corner < 3; corner++)
                        {
                            int i = indices[triangle+corner];
                            var p = Position(i);
                            var n = normals == null ? face : SafeNormal(Vector3.TransformNormal(new Vector3(normals[i][0],normals[i][1],normals[i][2]), normalMatrix));
                            var baseColor = tint;
                            if (colors != null) baseColor *= new Vector4(colors[i][0],colors[i][1],colors[i][2],colors[i].Length == 4 ? colors[i][3] : 1);
                            // Linear light then sRGB output keeps Blender's flat palette bright.
                            float light = .62f + .38f * Math.Max(0, Vector3.Dot(n, Vector3.Normalize(new Vector3(-3,6,4))));
                            var lit = new Vector3(Srgb(baseColor.X*light),Srgb(baseColor.Y*light),Srgb(baseColor.Z*light));
                            vertices[triangle+corner] = new(p, new Color(lit));
                            result.Min = Vector3.Min(result.Min,p); result.Max = Vector3.Max(result.Max,p);
                        }
                    }
                    result.Parts.Add(new(node.TryGetProperty("name", out var name) ? name.GetString()! : $"Node {index}",vertices));
                    Vector3 Position(int i) => Vector3.Transform(new Vector3(positions[i][0],positions[i][1],positions[i][2]),world);
                }
            if (node.TryGetProperty("children", out var children)) foreach (var child in children.EnumerateArray()) Visit(child.GetInt32(),world,ancestors);
            ancestors.Remove(index);
        }

        float[][] Read(int id)
        {
            var accessor = root.GetProperty("accessors")[id];
            if (accessor.TryGetProperty("sparse", out _)) throw new NotSupportedException("Sparse accessors are not supported.");
            var view = root.GetProperty("bufferViews")[accessor.GetProperty("bufferView").GetInt32()];
            if (Int(view,"buffer",0) != 0) throw new NotSupportedException("External buffers are not supported.");
            int type = accessor.GetProperty("componentType").GetInt32();
            int size = type switch { 5120 or 5121 => 1, 5122 or 5123 => 2, 5125 or 5126 => 4, _ => throw new NotSupportedException("Accessor component type.") };
            int width = accessor.GetProperty("type").GetString() switch { "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, _ => throw new NotSupportedException("Accessor shape.") };
            int stride = Int(view,"byteStride",size*width);
            int start = Int(view,"byteOffset",0)+Int(accessor,"byteOffset",0);
            bool normalized = accessor.TryGetProperty("normalized",out var normalizedValue) && normalizedValue.GetBoolean();
            var values = new float[accessor.GetProperty("count").GetInt32()][];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = new float[width];
                for (int j = 0; j < width; j++)
                {
                    int p = start+i*stride+j*size;
                    float value = type switch { 5120 => (sbyte)data[p], 5121 => data[p], 5122 => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(p,2)), 5123 => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(p,2)), 5125 => U32(data,p), 5126 => BitConverter.ToSingle(data,p), _ => 0 };
                    if (normalized) value = type switch { 5120 => Math.Max(value/127,-1), 5121 => value/255, 5122 => Math.Max(value/32767,-1), 5123 => value/65535, _ => value };
                    values[i][j] = value;
                }
            }
            return values;
        }
    }
    static uint U32(byte[] data,int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset,4));
    static int Int(JsonElement item,string key,int fallback) => item.TryGetProperty(key,out var value) ? value.GetInt32() : fallback;
    static Vector3 Vec(JsonElement item,string key,Vector3 fallback) => item.TryGetProperty(key,out var v) ? new(v[0].GetSingle(),v[1].GetSingle(),v[2].GetSingle()) : fallback;
    static Vector3 SafeNormal(Vector3 n) => n.LengthSquared() > 1e-12f ? Vector3.Normalize(n) : Vector3.Up;
    static float Srgb(float c) => c <= .0031308f ? c*12.92f : 1.055f*MathF.Pow(c,1/2.4f)-.055f;
}
