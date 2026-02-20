////////////////////////////////////////////////////////////////////////////////////////////
                            Stylized Shoot & Hit (by. Kyeoms)
////////////////////////////////////////////////////////////////////////////////////////////

Thank you for purchasing the Stylized Shoot & Hit Package.
This note describes how this package is configured, how texture should be used, and how it works within a Particle System.

This package is for Built-in, URP and HDRP.
To use in Built-in, you have to install "Shader Graph" from package manager and also your project version must be 2021.2.0 or higher.

All the effect prepab is made of only two materials(Additive and Alpha Blended). These materials use only one texture.
I put all the elements into that one texture.

   ▷ Red channel is main texture.
   ▷ Green channel is dissolve texture. The main texture gradually dissolve into the shape of green texture.
   ▷ Blue channel gives UV distortion.
   ▷ Alpha channel is for alpha.

These RGB channels can be modified by Custom Data in the Particle System.
There are few Components of Custom Data.

   ▷ Custon Data 1
      - X value is for Dissolve. From 0 to 1, it gradually dissolves.
      - Y value is for Dissolve Sharpness. The range is 0 ~ 0.5. The larger the number, the sharper the edges of dissolve.
      - Z value is for Distortion. The larger the number, the stronger the distortion.
      - W value is for Emissive Power. The larger the number, the more glows the texture.
   
   ▷ Custon Data 2
      - X value is for Soft Particle Factor. As the number goes to zero, the overlap of mesh becomes transparent.

There's the boolean parameter called "Use SoftParticle Factor?" in all materials.
If you use an Orthographic Camera, and if your project environment is 2D or 2D Experimental, set them off.

Material and shader named "VFX_lab" and "VFX_lab_object" are not used for effects. It was used in the background of Scene just to show the effect.

Special thanks to Gabriel Aguiar Prod.
https://www.youtube.com/watch?v=xenW67bXTgM&ab_channel=GabrielAguiarProd.
This tutorial was a great help in creating a demo scene.

Thank you once again, and I hope my effect will be useful for your development.
- Kyeoms