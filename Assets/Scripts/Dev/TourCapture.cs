using System;
using UnityEngine;
using PetShop.Core;

namespace PetShop.Dev
{
    /// <summary>
    /// Off-screen render path for <see cref="CameraTour"/>. It gives the tour camera the same
    /// post FX as the player's camera (via <see cref="PostFxSetup.AttachLayer"/>) and draws
    /// into an HDR target. The HDR result is then blitted into an LDR target, and the PNG is
    /// read back from that, so the output stays RGB24 as before. On a null graphics device the
    /// layer is skipped and the camera renders straight into the LDR target, as it did before
    /// post FX. <see cref="Dispose"/> releases every texture this class created.
    /// </summary>
    public sealed class TourCapture : IDisposable
    {
        /// <summary>Depth-buffer bits of the render target the camera draws into.</summary>
        private const int RenderDepthBits = 24;

        /// <summary>MSAA sample count of the render target the camera draws into.</summary>
        private const int RenderAntiAliasing = 2;

        /// <summary>Render format when post FX are attached: HDR, so bloom and ACES see real highlights.</summary>
        private const RenderTextureFormat HdrRenderFormat = RenderTextureFormat.DefaultHDR;

        /// <summary>LDR format the PNG pixels are read back from (and the render format when headless).</summary>
        private const RenderTextureFormat LdrRenderFormat = RenderTextureFormat.ARGB32;

        private readonly int           _width;
        private readonly int           _height;
        private readonly RenderTexture _render;
        private readonly RenderTexture _ldr;     // null when rendering LDR directly
        private readonly Texture2D     _pixels;

        /// <summary>True when the post-processing layer was attached (non-null device).</summary>
        public bool PostFx { get; }

        /// <summary>Attaches post FX to <paramref name="cam"/> where possible and allocates the targets.</summary>
        public TourCapture(Camera cam, int width, int height)
        {
            _width  = width;
            _height = height;

            PostFx = PostFxSetup.AttachLayer(cam);
            if (PostFx) cam.allowHDR = true;

            _render = new RenderTexture(width, height, RenderDepthBits, PostFx ? HdrRenderFormat : LdrRenderFormat)
                { antiAliasing = RenderAntiAliasing };
            _ldr    = PostFx ? new RenderTexture(width, height, 0, LdrRenderFormat) : null;
            _pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        }

        /// <summary>
        /// Points <paramref name="cam"/> at this capture's target, so a ScreenSpaceCamera canvas
        /// lays itself out at this capture's size on the frames before <see cref="RenderPng"/>.
        /// </summary>
        public void Bind(Camera cam) => cam.targetTexture = _render;

        /// <summary>Renders one frame through <paramref name="cam"/> and returns it PNG-encoded.</summary>
        public byte[] RenderPng(Camera cam)
        {
            cam.targetTexture = _render;
            cam.Render();
            cam.targetTexture = null;

            if (_ldr != null) Graphics.Blit(_render, _ldr);

            var previous = RenderTexture.active;
            RenderTexture.active = _ldr != null ? _ldr : _render;
            _pixels.ReadPixels(new Rect(0, 0, _width, _height), 0, 0);
            _pixels.Apply();
            RenderTexture.active = previous;

            return _pixels.EncodeToPNG();
        }

        /// <summary>Releases and destroys every texture this capture created.</summary>
        public void Dispose()
        {
            _render.Release();
            UnityEngine.Object.Destroy(_render);
            if (_ldr != null)
            {
                _ldr.Release();
                UnityEngine.Object.Destroy(_ldr);
            }
            UnityEngine.Object.Destroy(_pixels);
        }
    }
}
