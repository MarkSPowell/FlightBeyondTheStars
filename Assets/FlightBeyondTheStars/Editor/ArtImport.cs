using UnityEditor;
using UnityEngine;

namespace FBTS.EditorTools
{
    /// <summary>
    /// Import settings for everything under Resources/Art, applied automatically when a file is added or
    /// re-imported: mipmaps and trilinear filtering (so big painted sprites stay smooth when drawn small),
    /// repeat wrapping for textures that tile or scroll (Space/, Ground/, treads), and readable pixels for the planet
    /// detail texture (the game mixes it into each planet's map). Runs in the editor only.
    /// </summary>
    public class ArtImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.Contains("/Resources/Art/")) return;

            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.filterMode = FilterMode.Trilinear;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;

            bool tiles = path.Contains("/Art/Space/") || path.Contains("/Art/Ground/") || path.Contains("tread");
            ti.wrapMode = tiles ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.isReadable = path.Contains("planet_detail");
        }
    }
}
