using UnityEngine;

namespace Sabrevois.Utils
{
    public class AppointScribe : MonoBehaviour
    {
        private void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.unityLogger.logHandler = new Scribe();
#else
            Debug.unityLogger.logEnabled = false;
#endif
        }
    }
}
