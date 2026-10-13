using UnityEditor;
using UnityEngine;
using System.IO;

public class WrestlerTextureImport:AssetPostprocessor {
    void OnPreprocessTexture(){
        if(!assetPath.Contains("Resources/Wrestlers/"))return;
        var importer=(TextureImporter)assetImporter;
        // The exporter records normal names instead of inferring from pixels.
        var manifest=Path.Combine(Path.GetDirectoryName(assetPath),"normal-textures.txt");
        bool normal=File.Exists(manifest)&&File.ReadAllText(manifest).Contains(Path.GetFileNameWithoutExtension(assetPath));
        importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        importer.sRGBTexture=!normal;importer.mipmapEnabled=true;importer.anisoLevel=4;
        importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=2048;
        importer.textureCompression=TextureImporterCompression.CompressedHQ;
        importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=!normal;
    }
}
