using System;
using UnityEngine;

namespace DroneTrainer.Editor
{
    public static class ExamCourseChecks
    {
        public static void Run(TrainerApp app, Action<bool,string> check)
        {
            app.SetRole(RunRole.Local);
            var c=app.Course; var d=app.Drone;
            app.SelectProfile(ExamProfile.Under2kg);
            app.SelectPractice(PracticeKind.Hover); d.enabled=false; c.enabled=false;
            d.Takeoff(); Pose(d,new Vector3(0,1.82f,0),90);
            c.Advance(6); check(c.StepIndex==0,"wrong heading cannot pass initial hover");
            Pose(d,new Vector3(0,1.82f,0),0); c.Advance(4);
            Pose(d,new Vector3(2,1.82f,0),0); c.Advance(.1f);
            Pose(d,new Vector3(0,1.82f,0),0); c.Advance(1.1f);
            check(c.StepIndex==0,"leaving hover resets continuous five second timer");
            c.Advance(4); check(c.StepIndex==1,"stable hover advances after five continuous seconds");
            Pose(d,new Vector3(0,1.82f,0),270); c.Advance(.1f);
            Pose(d,new Vector3(0,1.82f,0),180); c.Advance(.1f);
            Pose(d,new Vector3(0,1.82f,0),90); c.Advance(6);
            check(c.StepIndex==1,"counterclockwise long turn cannot satisfy clockwise quarter turn");
            app.SelectPractice(PracticeKind.Hover); d.enabled=false; c.enabled=false; d.Takeoff();
            Pose(d,new Vector3(0,1.82f,0),0); c.Advance(5.1f);
            for(int i=1;i<=4;i++) { Pose(d,new Vector3(0,1.82f,0),i*90); c.Advance(5.1f); }
            check(c.StepIndex==5 && !c.Finished,"four rotations still require final landing");
            Pose(d,new Vector3(0,.32f,0),0); d.Land(); d.SendMessage("FixedUpdate"); c.Advance(.6f);
            check(c.Finished && c.Score>0,"correct four-facing sequence and landing completes");
            app.SelectPractice(PracticeKind.Landing); d.enabled=false; d.Takeoff();
            Pose(d,new Vector3(0,1.82f,0),0); c.Advance(5.1f);
            Pose(d,new Vector3(.8f,.32f,0),0); d.Land(); d.SendMessage("FixedUpdate"); c.Advance(.6f);
            check(c.Finished && c.Score==0,"gear crossing H line fails despite center inside");
            app.SelectPractice(PracticeKind.Square);
            check(c.StepCount>45,"rectangle contains both directions and sampled travel segments");
            foreach(ExamProfile profile in new[]{ExamProfile.BasicI,ExamProfile.BasicII,ExamProfile.BasicIII})
            {
                app.SelectProfile(profile); app.SelectPractice(PracticeKind.Circuit);
                d.enabled=false; d.Takeoff();
                float maxX=0,maxZ=0; int guard=0;
                while(!c.Finished && guard++<1500)
                {
                    var stage=c.CurrentStage;
                    maxX=Mathf.Max(maxX,Mathf.Abs(stage.point.x)); maxZ=Mathf.Max(maxZ,stage.point.z);
                    Pose(d,stage.point,stage.heading); d.Body.velocity=stage.moving?Vector3.forward:Vector3.zero;
                    c.Advance(stage.seconds+.1f);
                }
                check(c.Finished && c.StepIndex==c.StepCount && Mathf.Abs(maxX-40*c.Scale)<.01f && Mathf.Abs(maxZ-20*c.Scale)<.01f,"five-leg course dimensions and reachable targets for "+profile);
                app.SelectPractice(PracticeKind.Sideways);
                check(c.StepCount>35,"sideways course built for "+profile);
            }
            app.SelectProfile(ExamProfile.BasicI); app.SelectPractice(PracticeKind.Emergency); d.enabled=false; d.Takeoff();
            Pose(d,new Vector3(25,20.32f,10),180); c.TriggerEmergency();
            check(c.StepCount==3,"emergency replaces circuit with stabilization approach and landing");
            c.Advance(.1f); check(c.StepIndex==1 && c.CurrentStage.point.x==6,"emergency selects nearer right inner circle");
            Pose(d,new Vector3(6,1.82f,6),217); c.Advance(.1f);
            check(c.StepIndex==2,"emergency approach permits arbitrary heading without five second dwell");
            Pose(d,new Vector3(6,.32f,6),217); d.Land(); d.SendMessage("FixedUpdate"); c.Advance(.6f);
            check(c.Finished && c.Score>0,"emergency landing completes in correct inner circle");
            app.SelectPractice(PracticeKind.FigureEight);
            check(c.StepCount>145,"figure eight uses ordered continuous two-circle samples");
            app.SelectPractice(PracticeKind.Preflight); app.Command("takeoff");
            check(!d.Flying,"procedure card prevents takeoff");
            for(int i=0;i<4;i++) app.Command("confirm");
            check(c.Finished,"procedure acknowledgment finishes checklist");
            app.SelectPractice(PracticeKind.Hover); d.enabled=false; d.Takeoff();
            Pose(d,new Vector3(0,1.82f,-8),0); c.Advance(.1f);
            check(c.Finished && c.Score==0,"flight behind operator stops exercise");
            d.enabled=true; c.enabled=true; app.SelectProfile(ExamProfile.Under2kg); app.SelectPractice(PracticeKind.Hover);
        }
        static void Pose(DroneMotor d, Vector3 position,float yaw)
        {
            d.Body.position=position; d.Body.rotation=Quaternion.Euler(0,yaw,0);
            d.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
            d.Body.velocity=Vector3.zero; d.Body.angularVelocity=Vector3.zero; Physics.SyncTransforms();
        }
    }
}
