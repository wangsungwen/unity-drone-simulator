using UnityEngine;

namespace DroneTrainer
{
    public sealed class FlightCamera : MonoBehaviour
    {
        Light sun;
        void Awake() { sun = FindObjectOfType<Light>(); }
        public Transform target;
        public PracticeCourse Course;
        public float OperatorZ = -2.25f;
        public float OverviewZoom = 1;
        public int Mode { get; private set; }
        public string ModeName { get { return new[] { "全景俯視", "眼高操作員", "跟隨飛機", "機頭視角" }[Mode]; } }
        public void SetMode(int mode) { Mode = Mathf.Clamp(mode, 0, 3); }
        public void Cycle() { SetMode((Mode + 1) % 4); }
        public Bounds CourseBounds
        {
            get
            {
                float scale = Course == null ? 1 : Course.Scale;
                bool circuit = Course != null && (Course.Kind == PracticeKind.Circuit || Course.Kind == PracticeKind.Emergency);
                bool small = Course == null || Course.Profile == ExamProfile.Under2kg;
                return circuit ? new Bounds(new Vector3(0,10,10*scale),new Vector3(84*scale,22,24*scale))
                    : small ? new Bounds(new Vector3(0,1,2),new Vector3(17,3,12))
                    : new Bounds(new Vector3(0,1,6*scale),new Vector3(30*scale,3,18*scale));
            }
        }
        void LateUpdate()
        {
            if (target == null) return;
            var camera = GetComponent<Camera>();
            camera.orthographic = Mode == 0;
            RenderSettings.fog = Mode != 0;
            if (sun != null) sun.shadows = Mode == 0 ? LightShadows.None : LightShadows.Soft;
            if (Mode == 0)
            {
                // Fit the course into the clear center band, leaving room for edge controls.
                Bounds bounds = CourseBounds;
                Quaternion rotation = Quaternion.Euler(55,0,0);
                float vertical = 0, horizontal = 0;
                for (int i=0;i<8;i++)
                {
                    Vector3 corner = Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    Vector3 view = Quaternion.Inverse(rotation)*corner;
                    vertical = Mathf.Max(vertical,Mathf.Abs(view.y)); horizontal = Mathf.Max(horizontal,Mathf.Abs(view.x));
                }
                camera.orthographicSize = Mathf.Max(vertical/.46f,horizontal/(Mathf.Max(.5f,camera.aspect)*.88f))*Mathf.Clamp(OverviewZoom,.75f,1.5f);
                transform.SetPositionAndRotation(bounds.center-rotation*Vector3.forward*140,rotation);
                camera.farClipPlane=400;
                return;
            }
            camera.fieldOfView=60;
            Vector3 position, look;
            if (Mode == 1)
            { position = new Vector3(0,1.7f,OperatorZ); look = target.position; }
            else if (Mode == 3)
            { position = target.position+target.forward*.7f+Vector3.up*.1f; look = position+target.forward*10; }
            else
            { position = target.position+new Vector3(-5,6,-9); look = target.position+Vector3.up*.3f; }
            // Snap mode changes so the overview's long camera distance never sweeps through the field.
            transform.position=position;
            if ((look-position).sqrMagnitude>.01f) transform.rotation=Quaternion.LookRotation(look-position);
        }
    }
}
