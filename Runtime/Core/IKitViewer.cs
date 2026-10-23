using UnityEngine;

namespace Kit
{
    /// <summary>
    /// A camera that looks on behalf of a body (the kit's player camera, a student's own camera script). Nodes that
    /// work "from the player" (Benutzen: reach, own body ignored) ask the camera for it instead of guessing from the
    /// scene hierarchy, so a camera that is not a child of the player still works.
    /// </summary>
    public interface IKitViewer
    {
        /// <summary>The body the camera looks for, or null.</summary>
        Transform Body { get; }
    }
}
