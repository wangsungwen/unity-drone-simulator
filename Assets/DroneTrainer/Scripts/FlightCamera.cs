using UnityEngine;

namespace DroneTrainer
{
    public sealed class FlightCamera : MonoBehaviour
    {
        public Transform target;
        public int Mode { get; private set; }
        public string ModeName { get { return Mode == 0 ? "追蹤" : Mode == 1 ? "地面操作員" : "機頭視角"; } }
        public void Cycle() { Mode = (Mode + 1) % 3; }
        void LateUpdate()
        {
            if (target == null) return;
            Vector3 position, look;
            if (Mode == 1)
            { position = new Vector3(0, 2, -15); look = target.position; }
            else if (Mode == 2)
            { position = target.position + target.forward * .7f + Vector3.up * .1f; look = position + target.forward * 10; }
            else
            { position = target.position + new Vector3(-5, 4, -8); look = target.position + Vector3.up * .3f; }
            transform.position = Mode == 2 ? position : Vector3.Lerp(transform.position, position, 1 - Mathf.Exp(-5 * Time.deltaTime));
            if ((look - transform.position).sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(look - transform.position);
        }
    }
}
