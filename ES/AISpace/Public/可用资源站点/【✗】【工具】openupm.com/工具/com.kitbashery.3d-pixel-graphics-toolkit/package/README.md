<!-- ONLINE DOCUMENTATION FOUND @ https://kitbashery.com/docs/3d-pixel-graphics-toolkit -->



![](https://kitbashery.com/assets/images/kitbashery-github-banner.jpg)

[![Unity Version](https://img.shields.io/badge/Unity-2021.3%2B-blue.svg)](https://unity3d.com/get-unity/download)
[![GitHub](https://img.shields.io/github/license/kitbashery/modular-ai.svg)](https://github.com/Kitbashery/3D-Pixel-Graphics-Toolkit/blob/main/LICENSE.md)
[![OpenUPM](https://img.shields.io/badge/Install-openUPM-blue)](https://openupm.com/packages/com.kitbashery.3d-pixel-graphics-toolkit.html)
[![Latest](https://img.shields.io/badge/Manual%20Install-.unitypackage-blue)](https://github.com/Kitbashery/kitbashery.github.io/raw/main/assets/packages/3D-Pixel-Graphics-Toolkit.unitypackage)
[![AssetStore](https://img.shields.io/badge/Download%20LTS-Asset%20Store-blue)](https://assetstore.unity.com/packages/slug/233054)
[![Support Development](https://img.shields.io/badge/Ko--fi-Support%20Development-%23ff5f5f)](https://ko-fi.com/S6S8EKDY5)

# 3D Pixel Graphics Toolkit

HDRP shader graphs for creating pixel-style graphics in Unity 3D.

## Features:

* Pixelate Shader
* Pixel Cloud Shader
* Pixel Fluid Shader
* Pixel Triplanar Shader

### Pixel-Perfect Primitives:
Meshes with UVs optimized for pixel rendering.

* Cube
* Cylinder
* Ramp
* Sphere
* Dome

## Getting Started:

1. Import the package.
2. Import the demo scene found under the samples section in the package manager. (this will include the meshes).

Other things you may wish to do for a pixel-style project is to add an Import preset so imported textures have their import setting automatically set to:
* Max size set to "32".
* Filter Mode set to "Point (no filter)".
* Compression set to "none".
This will save a lot of time messing with the import settings.

It is also good to note that for 16x16 textures that one pixel in world units scale is `0.0625` and that magic number helps when snapping scale values acheive a uniform look.

It should also be noted that although primitive meshes such as the ramp, dome and sphere have UVs optimized for pixel perfect rendering there is geometric limitations that will still result in stretching. It is best to keep those meshes scaled uniformly, however checking `sloped surface` in the triplanar shader's material properties may help with some cases.

Also if you notice textures are not tiling then try using material instancing instead of static batching.

---- 
The name Kitbashery & all associated images Copyright &copy; 2022 Kitbashery. All Rights Reserved.
