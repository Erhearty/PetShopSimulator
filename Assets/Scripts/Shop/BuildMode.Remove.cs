using UnityEngine;
using PetShop.Core;

namespace PetShop.Shop
{
    /// <summary>
    /// The Remove tool: build mode open with no item, where LMB, middle-click or the remove key
    /// takes away whatever stands under the cursor (through <see cref="RemoveAtWorldPos"/>, so the
    /// removal listeners — NavMesh rebake, registry — fire as for any other removal). Also decides
    /// whether RMB cancels: in the top-down build view RMB belongs to the camera.
    /// </summary>
    public partial class BuildMode
    {
        /// <summary>Shown when the Remove tool is picked.</summary>
        public static string RemoveToolNotice => PetShop.Localization.Loc.F("build.remove_tool", "Esc");

        /// <summary>
        /// The top-down build view. While it shows, RMB is the camera's and does not cancel the
        /// current tool. Found on this object when not assigned.
        /// </summary>
        public BuildCamera BuildView { get; set; }

        /// <summary>True while the Remove tool is selected (build mode open, no <see cref="CurrentItem"/>).</summary>
        public bool IsRemoving { get; private set; }

        /// <summary>True when RMB drops the current tool: first person only, never in the build view.</summary>
        internal bool RightClickCancels => !InBuildView;

        private bool InBuildView
        {
            get
            {
                if (BuildView == null) BuildView = GetComponent<BuildCamera>();
                return BuildView != null && BuildView.IsActive;
            }
        }

        /// <summary>
        /// Opens build mode with the Remove tool. Any held item goes back to the inventory first;
        /// <see cref="OnBuildModeEntered"/> fires with a null item.
        /// </summary>
        public void EnterRemoveMode()
        {
            ReturnHeld();
            CurrentItem = null;
            IsRemoving  = true;
            IsActive    = true;
            CreateRemoveGhost();
            OnBuildModeEntered.Invoke(null);
            OnBuildMessage.Invoke(RemoveToolNotice);
        }

        /// <summary>One frame of the Remove tool: track the highlight, remove on click or key.</summary>
        private void UpdateRemoveTool()
        {
            UpdateRemoveGhost();
            if (InputBindings.GetKeyDown(GameAction.BuildRemove)) { TryRemoveUnderCursor(); return; }
            if (PointerOverUI) return;
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(2)) TryRemoveUnderCursor();
        }
    }
}
