using Godot;

namespace GameBox.Games.ConstructionSite;

internal static class SiteVisuals
{
    public static readonly Color Yellow = new("#f6c44e");
    public static readonly Color Orange = new("#f18d45");
    public static readonly Color Soil = new("#9b633c");
    public static readonly Color Dark = new("#344552");

    public static StandardMaterial3D Material(Color color, bool glow = false, float alpha = 1f)
        => new()
        {
            AlbedoColor = new Color(color.R, color.G, color.B, alpha),
            Roughness = .88f,
            Transparency = alpha < 1f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
            ShadingMode = glow ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel
        };

    public static MeshInstance3D Box(Node3D parent, Vector3 size, Vector3 position, Color color)
    {
        var node = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size }, Position = position, MaterialOverride = Material(color)
        };
        parent.AddChild(node);
        return node;
    }

    public static MeshInstance3D Cylinder(Node3D parent, float radius, float height, Vector3 position, Color color)
    {
        var node = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height },
            Position = position, MaterialOverride = Material(color)
        };
        parent.AddChild(node);
        return node;
    }

    public static MeshInstance3D Sphere(Node3D parent, float radius, Vector3 position, Color color)
    {
        var node = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = radius, Height = radius * 2f },
            Position = position, MaterialOverride = Material(color)
        };
        parent.AddChild(node);
        return node;
    }
}
