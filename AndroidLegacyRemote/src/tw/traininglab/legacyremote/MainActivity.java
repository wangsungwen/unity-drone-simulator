package tw.traininglab.legacyremote;

import android.app.Activity;
import android.os.Bundle;
import android.os.Handler;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.view.*;
import android.view.inputmethod.InputMethodManager;
import android.content.Context;
import android.widget.*;

public final class MainActivity extends Activity {
    private final RemoteClient client=new RemoteClient();
    private final Handler handler=new Handler();
    private TextView status, telemetry, task;
    private EditText address, code;
    private Stick left, right;
    private Button takeoff, land, reset, confirm, modeButton, sensitivityButton;
    private int mode=0;
    private float sensitivity=.65f;
    private boolean active;
    private final Runnable refresh=new Runnable() { public void run() {
        if (!active) return;
        status.setText(client.status); telemetry.setText(client.telemetry); task.setText(client.task);
        boolean ready=client.connected && !client.pending;
        takeoff.setEnabled(ready); land.setEnabled(ready); reset.setEnabled(ready); confirm.setEnabled(ready);
        if (!client.connected) clearSticks();
        handler.postDelayed(this,100);
    }};
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        LinearLayout root=column(); root.setPadding(dp(10),dp(4),dp(10),dp(4)); root.setBackgroundColor(0xff08161e);
        setContentView(root);
        TextView title=label("無人機輕量遙控器 • Android 5.0+",18); root.addView(title);
        LinearLayout connection=row(); root.addView(connection);
        address=new EditText(this); address.setTextSize(14); address.setSingleLine(); address.setHint("電腦 IPv4");
        address.setInputType(android.text.InputType.TYPE_CLASS_PHONE);
        address.setText(getPreferences(0).getString("address","")); connection.addView(address,new LinearLayout.LayoutParams(0,dp(48),3));
        code=new EditText(this); code.setTextSize(14); code.setSingleLine(); code.setHint("六位配對碼");
        code.setInputType(android.text.InputType.TYPE_CLASS_NUMBER); code.setFilters(new android.text.InputFilter[]{new android.text.InputFilter.LengthFilter(6)});
        connection.addView(code,new LinearLayout.LayoutParams(0,dp(48),2));
        connection.addView(button("連線",new Runnable(){ public void run(){
            clearSticks(); getPreferences(0).edit().putString("address",address.getText().toString().trim()).apply();
            ((InputMethodManager)getSystemService(Context.INPUT_METHOD_SERVICE)).hideSoftInputFromWindow(code.getWindowToken(),0);
            root.requestFocus(); client.connect(address.getText().toString(),code.getText().toString());
        }}),new LinearLayout.LayoutParams(dp(72),dp(48)));
        connection.addView(button("中斷",new Runnable(){ public void run(){ clearSticks(); client.disconnect(); }}),new LinearLayout.LayoutParams(dp(72),dp(48)));
        root.setFocusableInTouchMode(true); root.requestFocus();
        status=label("",13); root.addView(status); telemetry=label("",15); root.addView(telemetry);
        LinearLayout flight=row(); flight.setMotionEventSplittingEnabled(true); root.addView(flight,new LinearLayout.LayoutParams(-1,0,1));
        left=new Stick(true); right=new Stick(false);
        flight.addView(left,new LinearLayout.LayoutParams(0,-1,1));
        LinearLayout actions=column(); flight.addView(actions,new LinearLayout.LayoutParams(dp(150),-1));
        takeoff=command("起飛","takeoff"); land=command("降落","land"); confirm=command("程序確認","confirm");
        reset=button("重置（長按）",new Runnable(){ public void run(){ Toast.makeText(MainActivity.this,"長按重置以確認",Toast.LENGTH_SHORT).show(); }});
        reset.setOnLongClickListener(new View.OnLongClickListener(){ public boolean onLongClick(View v){ clearSticks(); client.request("reset"); return true; }});
        for (Button[] pair:new Button[][]{{takeoff,land},{confirm,reset}}) {
            LinearLayout line=row(); actions.addView(line,new LinearLayout.LayoutParams(-1,0,1));
            for(Button b:pair) line.addView(b,new LinearLayout.LayoutParams(0,-1,1));
        }
        flight.addView(right,new LinearLayout.LayoutParams(0,-1,1));
        LinearLayout options=row(); root.addView(options);
        modeButton=button("搖桿：Mode 2",new Runnable(){ public void run(){ clearSticks(); mode=(mode+1)%3; modeButton.setText("搖桿：Mode "+(mode==0?2:mode==1?1:3)); left.invalidate(); right.invalidate(); }});
        options.addView(modeButton,new LinearLayout.LayoutParams(0,dp(42),1));
        sensitivityButton=button("靈敏度：65%",new Runnable(){ public void run(){ clearSticks(); sensitivity=sensitivity<.5f?.65f:sensitivity<.9f?1:.4f; sensitivityButton.setText("靈敏度："+Math.round(sensitivity*100)+"%"); }});
        options.addView(sensitivityButton,new LinearLayout.LayoutParams(0,dp(42),1));
        task=label("",12); task.setMaxLines(2); root.addView(task);
    }
    private Button command(String text,final String command){ return button(text,new Runnable(){ public void run(){ client.request(command); }}); }
    private Button button(String text,final Runnable action){ Button b=new Button(this); b.setText(text); b.setTextSize(14); b.setAllCaps(false); b.setMinHeight(0); b.setMinimumHeight(0); b.setPadding(0,0,0,0); b.setOnClickListener(new View.OnClickListener(){ public void onClick(View v){ action.run(); }}); return b; }
    private LinearLayout column(){ LinearLayout l=new LinearLayout(this); l.setOrientation(LinearLayout.VERTICAL); return l; }
    private LinearLayout row(){ LinearLayout l=new LinearLayout(this); l.setOrientation(LinearLayout.HORIZONTAL); return l; }
    private TextView label(String text,int size){ TextView t=new TextView(this); t.setText(text); t.setTextSize(size); t.setTextColor(0xff99e5e3); return t; }
    private int dp(float value){ return Math.round(value*getResources().getDisplayMetrics().density); }
    private void clearSticks(){ if(left!=null)left.clear(); if(right!=null)right.clear(); publish(); }
    private void publish(){ if(left!=null && right!=null)client.input(left.x,left.y,right.x,right.y,mode,sensitivity); }
    @Override protected void onResume(){ super.onResume(); active=true; handler.post(refresh); }
    @Override protected void onPause(){ active=false; handler.removeCallbacks(refresh); clearSticks(); client.disconnect(); super.onPause(); }
    @Override public void onWindowFocusChanged(boolean focus){ super.onWindowFocusChanged(focus); if(!focus)clearSticks(); }
    @Override protected void onDestroy(){ client.destroy(); super.onDestroy(); }
    private final class Stick extends View {
        private final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG);
        private final boolean isLeft;
        private int pointer=-1;
        float x,y;
        Stick(boolean left){ super(MainActivity.this); isLeft=left; setContentDescription(left?"左搖桿":"右搖桿"); }
        void clear(){ pointer=-1; x=y=0; invalidate(); }
        float radius(){ return Math.max(1,Math.min(getWidth()/2f-dp(12),(getHeight()-dp(35))/2f)); }
        float cy(){ return (getHeight()-dp(25))/2f; }
        @Override protected void onDraw(Canvas c){
            float r=radius(),cx=getWidth()/2f,cy=cy(); paint.setColor(0xff183441); c.drawCircle(cx,cy,r,paint);
            paint.setColor(0xff41606a); c.drawLine(cx-r,cy,cx+r,cy,paint); c.drawLine(cx,cy-r,cx,cy+r,paint);
            paint.setColor(client.connected?0xff21cfc6:0xff65818a); c.drawCircle(cx+x*r*.7f,cy-y*r*.7f,r*.23f,paint);
            paint.setTextSize(dp(13)); paint.setTextAlign(Paint.Align.CENTER); paint.setColor(0xffd5ecec);
            String text;
            if(mode==0)text=isLeft?"左桿：升降 / 轉向":"右桿：前後 / 左右";
            else if(mode==1)text=isLeft?"左桿：前後 / 轉向":"右桿：升降 / 左右";
            else text=isLeft?"左桿：前後 / 左右":"右桿：升降 / 轉向";
            c.drawText(text,cx,getHeight()-dp(5),paint);
        }
        @Override public boolean onTouchEvent(MotionEvent e){
            int action=e.getActionMasked(),index=e.getActionIndex();
            if(action==MotionEvent.ACTION_CANCEL){ clear(); publish(); return true; }
            if(!client.connected){ clear(); publish(); return true; }
            if((action==MotionEvent.ACTION_DOWN || action==MotionEvent.ACTION_POINTER_DOWN) && pointer<0) pointer=e.getPointerId(index);
            if((action==MotionEvent.ACTION_UP || action==MotionEvent.ACTION_POINTER_UP) && e.getPointerId(index)==pointer){ clear(); publish(); performClick(); return true; }
            int i=e.findPointerIndex(pointer);
            if(i>=0){ x=(e.getX(i)-getWidth()/2f)/radius(); y=(cy()-e.getY(i))/radius(); float len=(float)Math.sqrt(x*x+y*y); if(len>1){x/=len;y/=len;} invalidate(); publish(); }
            return true;
        }
        @Override public boolean performClick(){ super.performClick(); return true; }
    }
}
