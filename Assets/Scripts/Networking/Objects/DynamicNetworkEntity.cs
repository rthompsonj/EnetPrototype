using NetStack.Serialization;
using SoL.Networking.Managers;
using UnityEngine;

namespace SoL.Networking.Objects
{
    public class DynamicNetworkEntity : NetworkEntity
    {
        private Vector4 m_cachedPosRot = Vector4.zero;
        protected Vector4? m_targetPosRot = null; 
        
        public override BitBuffer AddInitialState(BitBuffer outBuffer)
        {
            outBuffer = base.AddInitialState(outBuffer);
            outBuffer.AddVector3(gameObject.transform.position, BaseNetworkSystem.Range);
            outBuffer.AddFloat(gameObject.transform.eulerAngles.y);
            return outBuffer;
        }
    
        protected override BitBuffer ReadInitialState(BitBuffer inBuffer)
        {
            base.ReadInitialState(inBuffer);
            var pos = inBuffer.ReadVector3(BaseNetworkSystem.Range);
            var rot = Quaternion.Euler(new Vector3(0f, inBuffer.ReadFloat(), 0f));
            gameObject.transform.SetPositionAndRotation(pos, rot);
            return inBuffer;
        }

        protected override bool HasStateUpdate()
        {
            if (UseProximity && NObservers <= 0)
                return false;
            
           Vector4 currentPosRot = new Vector4(
               gameObject.transform.position.x, 
               gameObject.transform.position.y,
                gameObject.transform.position.z, 
               gameObject.transform.eulerAngles.y);

            if (currentPosRot == m_cachedPosRot)
                return false;

            m_cachedPosRot = currentPosRot;
            
            return true;
        }
        
        protected override BitBuffer AddStateUpdate(BitBuffer outBuffer)
        {
            outBuffer = base.AddStateUpdate(outBuffer);
            outBuffer.AddVector3(gameObject.transform.position, BaseNetworkSystem.Range);
            outBuffer.AddFloat(gameObject.transform.eulerAngles.y);
            return outBuffer;
        }
        
        protected override void ReadStateUpdate(BitBuffer inBuffer)
        {
            base.ReadStateUpdate(inBuffer);
            
            var pos = inBuffer.ReadVector3(BaseNetworkSystem.Range);
            var rot = Quaternion.Euler(new Vector3(0f, inBuffer.ReadFloat(), 0f));
            
            if (m_isServer)
            {
                gameObject.transform.SetPositionAndRotation(pos, rot);
            }
            else
            {
                m_targetPosRot = new Vector4(pos.x, pos.y, pos.z, rot.eulerAngles.y);
            }
        }

        protected virtual void Update()
        {
            LerpPositionRotation();
        }
        
        private void LerpPositionRotation()
        {            
            if (m_targetPosRot.HasValue)
            {
                var targetPos = Vector3.Lerp(gameObject.transform.position, m_targetPosRot.Value, Time.deltaTime * 2f);
                var targetRot = Quaternion.Lerp(gameObject.transform.rotation, Quaternion.Euler(new Vector3(0f, m_targetPosRot.Value.w, 0f)), Time.deltaTime * 2f);
                gameObject.transform.SetPositionAndRotation(targetPos, targetRot);
                if (Vector3.Distance(gameObject.transform.position, m_targetPosRot.Value) < 0.1f)
                {
                    m_targetPosRot = null;
                }
            }
        }
    }
}