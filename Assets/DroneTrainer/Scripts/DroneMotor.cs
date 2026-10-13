using UnityEngine;
using DroneTrainer.Core;

namespace DroneTrainer
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DroneMotor : MonoBehaviour
    {
        public const float GroundHeight = .32f;
        public FlightInput Input;
        public float windSpeed;
        public float windHeading = 90;
        public bool positionHold = true;
        public bool Flying { get; private set; }
        public bool Crashed { get; private set; }
        public bool Landing { get; private set; }
        public float Height { get { return Mathf.Max(0, transform.position.y - GroundHeight); } }
        public float Speed { get { return Body.velocity.magnitude; } }
        public Rigidbody Body { get; private set; }
        public Transform visual;
        public Transform[] propellers;
        Vector3 holdPosition;
        bool takingOff;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.mass = 1.2f;
            Body.useGravity = false;
            Body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            ResetFlight();
        }
        public void ResetFlight()
        {
            Flying = false; Crashed = false; Landing = false; takingOff = false;
            Input = default(FlightInput);
            Body.velocity = Vector3.zero; Body.angularVelocity = Vector3.zero;
            Body.position = new Vector3(0, GroundHeight, 0); Body.rotation = Quaternion.identity;
            holdPosition = Body.position;
            if (visual != null) visual.localRotation = Quaternion.identity;
        }
        public void Takeoff()
        {
            if (Crashed || Flying) return;
            Flying = true; takingOff = true; Landing = false;
            holdPosition = Body.position + Vector3.up * 1.5f;
        }
        public void Land() { if (Flying) { takingOff = false; Landing = true; } }
        public void Neutralize()
        {
            Input = default(FlightInput); takingOff = false; Landing = false;
            holdPosition = Body.position;
            if (!Body.isKinematic) { Body.velocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
        }
        void FixedUpdate()
        {
            if (!Flying || Crashed) { Body.velocity = Vector3.zero; return; }
            var input = Input.IsValid ? Input : default(FlightInput);
            Vector3 target = transform.TransformDirection(new Vector3(input.roll * 5, 0, input.pitch * 5));
            bool horizontalNeutral = Mathf.Abs(input.roll) + Mathf.Abs(input.pitch) < .001f;
            if (!horizontalNeutral || !positionHold) { holdPosition.x = Body.position.x; holdPosition.z = Body.position.z; }
            else
            {
                Vector3 correction = holdPosition - Body.position; correction.y = 0;
                target += Vector3.ClampMagnitude(correction * 1.8f, 3);
            }
            if (takingOff)
            {
                target.y = Mathf.Clamp((holdPosition.y - Body.position.y) * 1.5f, -.8f, 1.5f);
                if (Mathf.Abs(holdPosition.y - Body.position.y) < .1f) takingOff = false;
                if (Mathf.Abs(input.throttle) > .05f) takingOff = false;
            }
            else if (Landing) target.y = -.65f;
            else if (Mathf.Abs(input.throttle) > .001f)
            { target.y = input.throttle * 2.5f; holdPosition.y = Body.position.y; }
            else target.y = Mathf.Clamp((holdPosition.y - Body.position.y) * 2, -2, 2);

            if (Body.position.y >= 30 && target.y > 0) target.y = 0;
            Vector3 wind = Quaternion.Euler(0, windHeading, 0) * Vector3.forward * windSpeed;
            float gust = .85f + .15f * Mathf.Sin(Time.time * 1.7f);
            // Position hold rejects most wind; attitude practice allows continuous drift.
            target += wind * gust * (positionHold ? .08f : .6f);
            Body.AddForce(Vector3.ClampMagnitude((target - Body.velocity) * 3.5f, 9), ForceMode.Acceleration);
            Body.MoveRotation(Body.rotation * Quaternion.Euler(0, input.yaw * 85 * Time.fixedDeltaTime, 0));
            if (Height < .04f && (Landing || input.throttle < -.1f))
            { Flying = false; Landing = false; Body.velocity = Vector3.zero; }
            if (Mathf.Abs(Body.position.x) > 88 || Mathf.Abs(Body.position.z) > 88 || Body.position.y < -.5f)
            { Crashed = true; Flying = false; Body.velocity = Vector3.zero; }
        }
        void Update()
        {
            if (visual != null)
                visual.localRotation = Quaternion.Slerp(visual.localRotation,
                    Quaternion.Euler(Input.pitch * 16, 0, -Input.roll * 16), Time.deltaTime * 5);
            if (propellers == null) return;
            for (int i = 0; i < propellers.Length; i++)
                propellers[i].Rotate(0, (i % 2 == 0 ? 1 : -1) * (Flying ? 2100 : 0) * Time.deltaTime, 0);
        }
        void OnCollisionEnter(Collision collision)
        {
            if (!Flying) return;
            if (collision.gameObject.name != "Ground" || collision.relativeVelocity.magnitude > 3)
            { Crashed = true; Flying = false; Landing = false; Body.velocity = Vector3.zero; }
        }
    }
}
