using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace PetShop.Core
{
    /// <summary>
    /// Resources-loadable holder for the Post Processing Stack v2 shader/texture bundle.
    /// A <see cref="PostProcessLayer"/> added from code needs <see cref="PostProcessResources"/>
    /// passed to <see cref="PostProcessLayer.Init"/>, and the package's own asset is only
    /// reachable through the AssetDatabase in the editor. This asset (under Assets/Resources)
    /// references it so the bundle is pulled into the player build and found at runtime.
    /// </summary>
    public sealed class PostFxResourcesHolder : ScriptableObject
    {
        /// <summary>Path of the holder asset relative to a Resources folder, without extension.</summary>
        public const string ResourcePath = "PostFx/PostFxResources";

        [SerializeField] private PostProcessResources resources;

        /// <summary>The package's PostProcessResources asset, or null if the reference is missing.</summary>
        public PostProcessResources Resources => resources;

        /// <summary>Loads the holder from Resources and returns its bundle, or null when unavailable.</summary>
        public static PostProcessResources Load()
        {
            var holder = UnityEngine.Resources.Load<PostFxResourcesHolder>(ResourcePath);
            return holder != null ? holder.resources : null;
        }
    }
}
