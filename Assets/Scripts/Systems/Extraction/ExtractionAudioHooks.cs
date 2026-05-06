using System;
using UnityEngine;

namespace LingoteRush.Systems.Extraction
{
    public sealed class ExtractionAudioHooks : MonoBehaviour
    {
        [SerializeField] private bool logHookRequests;

        public event Action<float> ImpactCueRequested;
        public event Action<Vector3, float> NuggetLandingCueRequested;

        public void NotifyImpact(float impactForce)
        {
            ImpactCueRequested?.Invoke(impactForce);

            if (logHookRequests)
            {
                Debug.Log($"Impact cue requested with force {impactForce:F2}.");
            }
        }

        public void RegisterNugget(GoldNugget nugget)
        {
            if (nugget == null)
            {
                return;
            }

            nugget.Landed -= HandleNuggetLanded;
            nugget.Landed += HandleNuggetLanded;
        }

        private void HandleNuggetLanded(GoldNugget nugget, Collision collision)
        {
            if (collision.contactCount <= 0)
            {
                return;
            }

            var contactPoint = collision.GetContact(0).point;
            NuggetLandingCueRequested?.Invoke(contactPoint, collision.relativeVelocity.magnitude);

            if (logHookRequests)
            {
                Debug.Log($"Nugget landing cue requested at {contactPoint}.");
            }

            if (nugget != null)
            {
                nugget.Landed -= HandleNuggetLanded;
            }
        }
    }
}
