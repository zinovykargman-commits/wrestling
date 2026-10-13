using UnityEditor;
using UnityEngine;
public class GymTextureImport:AssetPostprocessor {
    void OnPreprocessTexture(){
        if(!assetPath.Contains("Resources/Gym/"))return;
        var importer=(TextureImporter)assetImporter;
        if(assetPath.EndsWith(".hdr")){importer.textureShape=TextureImporterShape.TextureCube;importer.generateCubemap=TextureImporterGenerateCubemap.AutoCubemap;importer.maxTextureSize=512;importer.sRGBTexture=false;return;}
        importer.textureType=assetPath.Contains("_normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
        importer.sRGBTexture=assetPath.Contains("_color");importer.mipmapEnabled=true;importer.anisoLevel=8;
        importer.wrapMode=TextureWrapMode.Repeat;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;
        importer.alphaSource=TextureImporterAlphaSource.FromInput;
    }
}
