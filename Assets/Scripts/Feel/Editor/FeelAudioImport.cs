// ============================================================================
// FeelAudioImport.cs   (Editor only)
// Gives every new sound in Assets/Audio the right import settings for the web build, so
// nobody has to set them by hand:
//   SFX (Assets/Audio/SFX/...) and Stingers: Decompress On Load (instant to play), Vorbis.
//       Long ones (over about 8 s) use Compressed In Memory instead, to save memory.
//       Player, Hub, Gates and Maze sounds are made mono (they will play in 3D).
//   Ambience and music (Assets/Audio/Ambience, Assets/Audio/BGM): Compressed In Memory, Vorbis, loads in the
//       background; very long tracks Stream instead.
// Only applied the FIRST time a file is imported. If an artist changes a sound's settings
// afterwards, those changes are kept. Files in Assets/Audio/_Inbox are left alone.
//
// DESIGN RULES: none (tooling).
//
// HOW TO USE: nothing to do; drop WAVs into the folders. To apply again to a file:
// right-click it > Reimport after deleting its .meta file (or set it by hand).
// HOW TO EDIT: the numbers are the constants below.
// ============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastDrop.Feel.EditorTools
{
    /// <summary>Sets web-friendly import settings on new audio under Assets/Audio.</summary>
    public class FeelAudioImport : AssetPostprocessor
    {
        const string Root = "Assets/Audio/";
        const float SfxQuality = 0.7f;        // Vorbis quality 0..1 for sound effects
        const float AmbienceQuality = 0.5f;   // Vorbis quality 0..1 for ambience loops
        const long LongSfxBytes = 1_500_000;  // ≈ 8.5 s of 44.1 kHz stereo WAV
        const long StreamBytes = 12_000_000;  // ≈ 70 s of 44.1 kHz stereo WAV

        /// <summary>Folders whose sounds will be played in 3D (made mono).</summary>
        static readonly string[] MonoFolders = { "SFX/Player/", "SFX/Hub/", "SFX/Gates/", "SFX/Maze/" };

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root) || assetPath.Contains("/_Inbox/")) return;
            if (!assetImporter.importSettingsMissing) return; // only on first import: keep later hand edits

            var importer = (AudioImporter)assetImporter;
            // Options waiting for a pick (_Candidates/...) get the same rules as their final folder.
            string local = assetPath.Substring(Root.Length).Replace("_Candidates/", "");
            long bytes = 0;
            try { bytes = new FileInfo(assetPath).Length; } catch (IOException) { }

            var s = importer.defaultSampleSettings;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;

            if (local.StartsWith("Ambience/") || local.StartsWith("BGM/"))
            {
                s.loadType = bytes > StreamBytes ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory;
                s.quality = AmbienceQuality;
                s.preloadAudioData = false;
                importer.loadInBackground = true;
                importer.forceToMono = false;
            }
            else
            {
                s.loadType = bytes > LongSfxBytes ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                s.quality = SfxQuality;
                s.preloadAudioData = true;
                importer.loadInBackground = false;
                importer.forceToMono = IsMonoFolder(local);
            }
            importer.defaultSampleSettings = s;
        }

        static bool IsMonoFolder(string local)
        {
            for (int i = 0; i < MonoFolders.Length; i++)
                if (local.StartsWith(MonoFolders[i])) return true;
            return false;
        }
    }
}
