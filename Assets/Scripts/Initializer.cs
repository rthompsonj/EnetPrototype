using ENet;
using UnityEngine;

public class Initializer : MonoBehaviour
{
    void Awake()
    {
        Library.Initialize();
    }

    private void OnDestroy()
    {
        Library.Deinitialize();
    }
}