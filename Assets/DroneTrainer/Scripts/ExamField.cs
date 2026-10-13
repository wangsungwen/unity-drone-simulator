using UnityEngine;

namespace DroneTrainer
{
    // Source dimensions: AC 107-005D, appendix 6, PDF pp.232–234.
    public static class ExamField
    {
        public static GameObject Build(ExamProfile profile, PracticeKind kind)
        {
            var root = new GameObject("CAA practice markings");
            float s = DroneTrainer.Core.ExamRules.Scale((int)profile);
            bool small = profile == ExamProfile.Under2kg;
            Color white = new Color(.92f,.95f,.9f), yellow = new Color(1,.78f,.12f);
            Ring(root.transform,Vector3.zero,small ? 1 : s,white);
            Label(root.transform,"H",new Vector3(0,.05f,0));
            if (small)
            {
                Path(root.transform,new[] { V(-7.5f,-6.5f), V(-7.5f,8.5f), V(7.5f,8.5f), V(7.5f,-6.5f), V(-7.5f,-6.5f) },white);
                Path(root.transform,new[] { V(-1,-1),V(-7,-1),V(-7,6),V(7,6),V(7,-1),V(1,-1) },yellow);
                Path(root.transform,new[] { V(-5,1),V(-5,4),V(5,4),V(5,1),V(-5,1) },yellow);
                Mark(root.transform,"P1",-6,0); Mark(root.transform,"P2",-6,5); Mark(root.transform,"P3",6,5); Mark(root.transform,"P4",6,0);
            }
            else
            {
                for (int side=-1; side<=1; side+=2)
                {
                    Ring(root.transform,V(side*6*s,6*s),4*s,yellow);
                    Ring(root.transform,V(side*6*s,6*s),8*s,white);
                }
                Mark(root.transform,"P1",-6*s,12*s); Mark(root.transform,"P2",-12*s,6*s); Mark(root.transform,"P3",-6*s,0);
                Mark(root.transform,"P4",0,6*s); Mark(root.transform,"P5",6*s,12*s); Mark(root.transform,"P6",12*s,6*s); Mark(root.transform,"P7",6*s,0);
                if (kind == PracticeKind.Circuit || kind == PracticeKind.Emergency)
                    Path(root.transform,new[] { V(-40*s,0),V(-40*s,20*s),V(40*s,20*s),V(40*s,0),V(-40*s,0) },new Color(.2f,.85f,.8f));
            }
            float operatorZ = small ? -2.25f : -7*s;
            Path(root.transform,new[] { V(-90,operatorZ),V(90,operatorZ) },new Color(1,.25f,.2f));
            Label(root.transform,"操作員 / 後方禁入",new Vector3(0,.04f,operatorZ-.7f));
            return root;
        }
        static Vector3 V(float x,float z) { return new Vector3(x,.035f,z); }
        static void Mark(Transform root,string name,float x,float z)
        {
            var marker=SceneFactory.Shape(name,PrimitiveType.Cylinder,new Vector3(x,.11f,z),new Vector3(.22f,.11f,.22f),new Color(1,.5f,.1f));
            marker.transform.SetParent(root,true);
            Label(root,name,new Vector3(x+.5f,.04f,z));
        }
        static void Ring(Transform root,Vector3 center,float radius,Color color)
        {
            var p=new Vector3[97];
            for(int i=0;i<p.Length;i++) { float a=i*Mathf.PI*2/96; p[i]=V(center.x+radius*Mathf.Cos(a),center.z+radius*Mathf.Sin(a)); }
            Path(root,p,color);
        }
        static void Path(Transform root,Vector3[] points,Color color)
        {
            var go=new GameObject("Exam marking"); go.transform.SetParent(root,false);
            var line=go.AddComponent<LineRenderer>(); line.sharedMaterial=SceneFactory.MakeMaterial(color,true);
            line.widthMultiplier=.05f; line.positionCount=points.Length; line.SetPositions(points);
        }
        static void Label(Transform root,string name,Vector3 position)
        {
            var go=new GameObject(name); go.transform.SetParent(root,false); go.transform.position=position;
            go.transform.rotation=Quaternion.Euler(90,0,0);
            var text=go.AddComponent<TextMesh>(); text.text=name; text.font=Resources.Load<Font>("TrainerFont");
            text.characterSize=.12f; text.fontSize=48; text.anchor=TextAnchor.MiddleCenter; text.color=Color.white;
            if(text.font!=null) go.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
        }
    }
}
