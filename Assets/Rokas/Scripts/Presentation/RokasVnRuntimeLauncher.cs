using System;
using UnityEngine;

namespace Rokas.Presentation
{
    /// <summary>
    /// Reusable bridge between a caller-owned game flow and a packaged VN player.
    /// The caller supplies the package and the continuation; gameplay routing stays outside VN.
    /// </summary>
    public sealed class RokasVnRuntimeLauncher : IDisposable
    {
        private readonly Transform parent;
        private RokasVnRuntimePlayer activePlayer;
        private bool disposed;

        public RokasVnRuntimePlayer ActivePlayer => activePlayer;
        public bool HasActivePlayer => activePlayer != null;

        public RokasVnRuntimeLauncher(Transform parentTransform)
        {
            parent = parentTransform
                ? parentTransform
                : throw new ArgumentNullException(nameof(parentTransform));
        }

        public bool TryPlay(
            RokasVnRuntimeIntroPackage package,
            Action onCompleted)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(RokasVnRuntimeLauncher));
            if (activePlayer != null) return false;
            if (!package) throw new ArgumentNullException(nameof(package));

            activePlayer = RokasVnRuntimePlayer.Create(
                parent,
                package,
                () =>
                {
                    Action continuation = onCompleted;
                    if (continuation != null) continuation();
                });
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (activePlayer != null)
            {
                activePlayer.Dispose();
                activePlayer = null;
            }
        }
    }
}
