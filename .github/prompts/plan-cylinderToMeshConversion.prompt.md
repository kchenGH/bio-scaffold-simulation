## Plan: Convert Cylinder Skeleton to Continuous Mesh

Transform the discrete cylinder scaffold (image 171040) into a unified, smooth biological mesh (image 175741) by replacing noise-based density generation with cylinder-based Signed Distance Fields (SDF), leveraging the existing marching cubes pipeline in `MeshGenerator.cs`.

### Steps

1. **Extend `ScaffoldConnector.cs` with cylinder data export** — Add `GetCylinderData()` method that returns `List<(Vector3 start, Vector3 end, float radius)>` containing all cylinder strut coordinates instead of instantiating GameObjects. Keep node tracking logic but skip GameObject creation.

2. **Implement capsule SDF function in `MeshGenerator.cs`** — Add `SDFCapsule(Vector3 p, Vector3 a, Vector3 b, float radius)` that calculates shortest distance from point to line segment minus radius. Use clamped projection formula: `h = Clamp01(Dot(pa, ba) / Dot(ba, ba))`.

3. **Replace noise-based density with cylinder SDF loop** — In `BuildDensityField()`, remove Worley/Perlin noise calls. Instead, iterate through all cylinders from `connectorReference.GetCylinderData()` and compute `minDist` using `SDFCapsule()`. Store negative distances (inside surface) in `densityField[ix, iy, iz]`.

4. **Add cross-component references** — Create public `ScaffoldConnector connectorReference` field in `MeshGenerator.cs`. Wire up the reference in Unity Inspector so mesh generator can access cylinder data after `ScaffoldGenerator` creates nodes.

5. **Tune parameters for smooth cylinder meshing** — Reduce `spacing` to 0.8-1.2 for higher resolution near thin struts. Adjust `poreThreshold` to 0 (SDF isosurface). Keep existing Gaussian blur passes—they'll naturally blend cylinder joints into organic connections.

6. **Optional: Add smooth minimum blending** — Implement `SmoothMin(float d1, float d2, float k)` function for metaball-style blending at cylinder junctions. Replace `Mathf.Min` with `SmoothMin(minDist, dist, 0.3f-0.8f)` in SDF loop for more biological appearance.

### Further Considerations

1. **Performance vs Quality trade-off?** — CPU-side SDF works for ~500 cylinders. For larger scaffolds (1000+), port SDF calculation to `MarchingCubes.compute` shader using `StructuredBuffer<float3>` for 10-50× speedup. Requires compute shader modification but reuses existing GPU infrastructure.

2. **Wall collision handling?** — Current approach checks collisions during node generation. Consider moving `Physics.CheckSphere` and raycasts to density field loop, setting `densityField = 1.0f` (outside) for voxels inside obstacles.

3. **Blend radius parameter exposure?** — If implementing smooth minimum, expose `blendRadius` slider (0.3-0.8 range) for artist control over joint smoothness. Lower values preserve structural definition; higher creates blobby organic joints.
