using System;
using UnityEngine;

namespace Spotlight
{
    public sealed class RunSession : MonoBehaviour
    {
        public string RunId { get; private set; } = string.Empty;
        public int CurrentCarriageIndex { get; private set; } = 1;
        public bool HasStarted { get; private set; }

        public void BeginNewRun()
        {
            RunId = Guid.NewGuid().ToString("N");
            CurrentCarriageIndex = 1;
            HasStarted = true;
        }
    }
}
