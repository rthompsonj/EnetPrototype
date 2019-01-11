using System;
using System.Collections;
using System.Collections.Generic;

namespace SoL.Networking.Proximity
{
    [Flags]
    public enum SensorBand
    {
        None = 0,
        A = 1 << 0,
        B = 1 << 1,
        C = 1 << 2,
        D = 1 << 3,
        E = 1 << 4
    }
    
    public static class ProximityExtensions
    {
        public static SensorBand SetFlag(this SensorBand a, SensorBand b)
        {
            return a | b;
        }

        public static SensorBand UnsetFlag(this SensorBand a, SensorBand b)
        {
            return a & (~b);
        }

        public static bool HasFlag(this SensorBand a, SensorBand b)
        {
            return (a & b) == b;
        }

        public static float GetUpdateTime(this SensorBand band)
        {
            switch (band)
            {
                case SensorBand.A:
                    return 1f / 10f;

                case SensorBand.B:
                    return 1f / 5f;

                case SensorBand.C:
                    return 1f / 2f;

                case SensorBand.D:
                    return 1f;

                case SensorBand.E:
                    return 2f;
                
                default:
                    throw new ArgumentException($"no time set for band {band.ToString()}!");
            }
        }
    }
    
    public struct ProximitySensorComparer : IEqualityComparer<ProximitySensor>, IComparer<ProximitySensor>
    {
        public bool Equals(ProximitySensor x, ProximitySensor y)
        {
            return x == y;
        }

        public int GetHashCode(ProximitySensor obj)
        {
            return obj.GetHashCode();
        }

        public int Compare(ProximitySensor x, ProximitySensor y)
        {
            return x.SensorBand.CompareTo(y.SensorBand);
        }
    }
}