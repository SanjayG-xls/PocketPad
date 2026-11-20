package io.pocketpad.app;

import android.Manifest;
import android.app.*;
import android.animation.*;
import android.content.*;
import android.content.pm.ActivityInfo;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.hardware.*;
import android.net.Uri;
import android.os.*;
import android.view.*;
import android.view.inputmethod.InputMethodManager;
import android.webkit.*;
import android.widget.*;
import org.json.JSONObject;
import java.io.*;
import java.net.*;
import java.util.Locale;
import java.util.concurrent.*;

public final class MainActivity extends Activity implements SensorEventListener {
    private static final int SCAN=42;
    private int BG=0xff111216,TEXT=0xffededf2,MUTED=0xff8f98ae,ACCENT=0xffa7adff,PANEL=0xff191c23;
    private final Handler main=new Handler(Looper.getMainLooper());
    private final ExecutorService network=Executors.newSingleThreadExecutor();
    private final float[] rotation=new float[9], remapped=new float[9], orientation=new float[3];
    private SharedPreferences prefs;
    private FrameLayout root;
    private WebView browser;
    private SensorManager sensors;
    private Sensor motionSensor,gyroSensor;
    private String origin="", motionMode="off";
    private String practiceMotionBefore=null;
    private boolean active=false, centered=false, connecting=false, desktopRequested=false;
    private float centerRoll,centerPitch,currentRoll,currentPitch;
    private float yawRate,pitchRate;
    private long lastGyro=0;
    private long lastMotion=0,lastHaptic=0;
    private TextView homeStatus;
    private ImageView homeMark;
    private AnimatorSet homeMotion;

    @Override public void onCreate(Bundle saved){
        super.onCreate(saved);setTheme(R.style.AppTheme);
        prefs=getSharedPreferences("pocketpad",MODE_PRIVATE);
        palette();
        sensors=(SensorManager)getSystemService(SENSOR_SERVICE);
        motionSensor=sensors.getDefaultSensor(Sensor.TYPE_GAME_ROTATION_VECTOR);
        if(motionSensor==null)motionSensor=sensors.getDefaultSensor(Sensor.TYPE_ROTATION_VECTOR);
        gyroSensor=sensors.getDefaultSensor(Sensor.TYPE_GYROSCOPE);
        motionMode=prefs.getString("motion","off");
        root=new FrameLayout(this);root.setBackgroundColor(BG);setContentView(root);
        applyInsets();
        if(saved==null&&prefs.getBoolean("animations",true)){LaunchView launch=new LaunchView(this,BG,ACCENT);root.addView(launch);launch.play(this::showHome);}else showHome();
    }
    private void applyInsets(){
        root.setOnApplyWindowInsetsListener((v,insets)->{
            if(Build.VERSION.SDK_INT>=30){android.graphics.Insets bars=insets.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout());v.setPadding(bars.left,bars.top,bars.right,bars.bottom);}
            else v.setPadding(insets.getSystemWindowInsetLeft(),insets.getSystemWindowInsetTop(),insets.getSystemWindowInsetRight(),insets.getSystemWindowInsetBottom());
            return insets;
        });root.requestApplyInsets();
    }
    private int dp(float n){return Math.round(n*getResources().getDisplayMetrics().density);}
    private GradientDrawable box(int color,int stroke,int radius){GradientDrawable bg=new GradientDrawable();bg.setColor(color);bg.setCornerRadius(dp(radius));if(stroke!=0)bg.setStroke(dp(1),stroke);return bg;}
    private TextView text(String value,int size,int color){TextView t=new TextView(this);t.setText(value);t.setTextSize(size);t.setTextColor(color);t.setFontFeatureSettings("kern");return t;}
    private Button button(String label,boolean primary,Runnable click){
        Button b=new Button(this);b.setText(label);b.setTextSize(14);b.setAllCaps(false);b.setTextColor(primary?BG:TEXT);b.setTypeface(Typeface.create("sans-serif-medium",Typeface.NORMAL));b.setBackground(box(primary?ACCENT:PANEL,primary?0:0xff343b4a,10));b.setPadding(dp(16),dp(8),dp(16),dp(8));b.setOnClickListener(v->{haptic();click.run();});return b;
    }
    private void add(LinearLayout parent,View v,int height,int top){LinearLayout.LayoutParams lp=new LinearLayout.LayoutParams(-1,height<0?height:dp(height));lp.topMargin=dp(top);parent.addView(v,lp);}
    private void status(String message){if(homeStatus!=null)homeStatus.setText(message);else Toast.makeText(this,message,Toast.LENGTH_LONG).show();}

    private void disposeBrowser(){
        if(browser==null)return;
        endPracticeMotion();
        browser.evaluateJavascript("window.dispatchEvent(new Event('pocketpad-pause'))",null);
        browser.removeJavascriptInterface("PocketPadNative");browser.stopLoading();browser.destroy();browser=null;
        sensors.unregisterListener(this);getWindow().clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
    }
    private void showHome(){
        stopHomeMotion();palette();root.setBackgroundColor(BG);
        disposeBrowser();origin="";connecting=false;setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED);
        if(Build.VERSION.SDK_INT>=30){WindowInsetsController controller=getWindow().getInsetsController();if(controller!=null)controller.show(WindowInsets.Type.systemBars());}
        else getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_LAYOUT_STABLE);
        root.removeAllViews();
        ScrollView scroll=new ScrollView(this);scroll.setFillViewport(true);scroll.setClipToPadding(false);
        LinearLayout content=new LinearLayout(this);content.setOrientation(LinearLayout.VERTICAL);content.setGravity(Gravity.CENTER_HORIZONTAL);content.setPadding(dp(28),dp(26),dp(28),dp(28));scroll.addView(content);
        LinearLayout header=new LinearLayout(this);header.setGravity(Gravity.CENTER_VERTICAL);
        TextView name=text("PocketPad",19,TEXT);name.setTypeface(Typeface.create("sans-serif-medium",0));header.addView(name,new LinearLayout.LayoutParams(0,-2,1));
        Button settings=button("Settings",false,this::showSettings);header.addView(settings);add(content,header,40,0);
        ImageView mark=new ImageView(this);homeMark=mark;mark.setImageResource(R.drawable.ic_mark);LinearLayout.LayoutParams iconLp=new LinearLayout.LayoutParams(dp(160),dp(160));iconLp.topMargin=dp(36);content.addView(mark,iconLp);
        TextView title=text("Connect to your PC",27,TEXT);title.setGravity(Gravity.CENTER);title.setTypeface(Typeface.create("sans-serif-medium",0));add(content,title,-2,12);
        TextView description=text("Open PocketPad on your PC. Connect both devices to the same network, then scan its QR code.",13,MUTED);description.setGravity(Gravity.CENTER);description.setLineSpacing(dp(4),1);add(content,description,-2,13);
        add(content,button("Scan QR code",true,()->{desktopRequested=false;scan();}),54,30);
        String previous=prefs.getString("last_url","");
        if(!previous.isEmpty())add(content,button("Reconnect to "+Uri.parse(previous).getHost(),false,()->connect(previous)),52,12);
        add(content,button("Virtual mouse & keyboard",false,()->{
            desktopRequested=true;
            if(!previous.isEmpty())connect(previous);else scan();
        }),52,12);
        add(content,button("Enter pairing link",false,()->{desktopRequested=false;manualEntry();}),52,12);
        Button preview=button("Preview controllers",false,()->openController("https://pocketpad.local/play?demo=1",true));add(content,preview,52,12);
        add(content,button("Getting-started walkthrough",false,()->openController("https://pocketpad.local/play?demo=1&tour=1",true)),48,18);
        homeStatus=text("Wi-Fi · USB tethering · Bluetooth PAN",11,MUTED);homeStatus.setGravity(Gravity.CENTER);homeStatus.setLineSpacing(dp(4),1);add(content,homeStatus,-2,20);
        TextView credit=text("Made by Sanjay",11,MUTED);credit.setGravity(Gravity.CENTER);add(content,credit,-2,16);
        root.addView(scroll,new FrameLayout.LayoutParams(-1,-1));
        if(prefs.getBoolean("animations",true)){
            for(int i=0;i<content.getChildCount();i++){View child=content.getChildAt(i);child.setAlpha(0);child.setTranslationY(dp(24));child.animate().alpha(1).translationY(0).setStartDelay(i*38).setDuration(600).setInterpolator(new android.view.animation.OvershootInterpolator(.55f)).start();}
            animateHomeMark();
        }
        if(!prefs.getBoolean("walkthroughPromptSeen",false)){
            prefs.edit().putBoolean("walkthroughPromptSeen",true).apply();
            main.postDelayed(()->{if(!isFinishing()&&active&&browser==null)new AlertDialog.Builder(this).setTitle("Get to know PocketPad").setMessage("Try pairing, resizing, two-finger touch and gyro in a short interactive guide. Practice never sends inputs to a PC game.").setPositiveButton("Start guide",(d,w)->openController("https://pocketpad.local/play?demo=1&tour=1",true)).setNegativeButton("Skip guide",(d,w)->prefs.edit().putBoolean("tourComplete",true).apply()).show();},prefs.getBoolean("animations",true)?650:0);
        }
    }
    private void scan(){
        if(checkSelfPermission(Manifest.permission.CAMERA)!=PackageManager.PERMISSION_GRANTED){requestPermissions(new String[]{Manifest.permission.CAMERA},SCAN);return;}
        startActivityForResult(new Intent(this,ScanActivity.class),SCAN);
    }
    @Override public void onRequestPermissionsResult(int code,String[] permissions,int[] results){super.onRequestPermissionsResult(code,permissions,results);if(code==SCAN){if(results.length>0&&results[0]==PackageManager.PERMISSION_GRANTED)scan();else status("Camera access was declined. You can enter the pairing link instead.");}}
    @Override protected void onActivityResult(int request,int result,Intent data){super.onActivityResult(request,result,data);if(request==SCAN&&result==RESULT_OK&&data!=null)connect(data.getStringExtra("url"));}
    private void manualEntry(){
        EditText field=new EditText(this);field.setTextColor(TEXT);field.setTextSize(13);field.setSingleLine(true);field.setInputType(android.text.InputType.TYPE_CLASS_TEXT|android.text.InputType.TYPE_TEXT_VARIATION_URI);field.setHint("http://192.168.1.5:8765/play?token=…");field.setHintTextColor(MUTED);field.setPadding(dp(18),dp(16),dp(18),dp(16));
        new AlertDialog.Builder(this).setTitle("Pairing link").setMessage("Copy the full phone link from PocketPad’s Connection screen.").setView(field).setNegativeButton("Cancel",null).setPositiveButton("Connect",(d,w)->connect(field.getText().toString())).show();
    }
    private void connect(String input){
        if(connecting)return;
        final Uri uri;
        try{Uri parsed=Uri.parse(input.trim());if(!"http".equals(parsed.getScheme())&&!"https".equals(parsed.getScheme()))throw new Exception();if(parsed.getHost()==null||!"/play".equals(parsed.getPath())||parsed.getQueryParameter("token")==null)throw new Exception();uri=desktopRequested?parsed.buildUpon().appendQueryParameter("input","desktop").build():parsed;desktopRequested=false;}
        catch(Exception e){status("That is not a PocketPad pairing link. Scan the QR code on your PC.");return;}
        connecting=true;status("Connecting to "+uri.getHost()+"…");connectionHaptic("connecting");
        network.execute(()->{
            HttpURLConnection request=null;
            try{
                URL check=new URL(uri.getScheme(),uri.getHost(),uri.getPort(),"/api/info");
                request=(HttpURLConnection)check.openConnection();request.setConnectTimeout(4000);request.setReadTimeout(4000);
                if(request.getResponseCode()!=200)throw new IOException("PC host unavailable");
                ByteArrayOutputStream buffer=new ByteArrayOutputStream();try(InputStream in=request.getInputStream()){byte[] part=new byte[4096];int n;while((n=in.read(part))!=-1){buffer.write(part,0,n);if(buffer.size()>256*1024)throw new IOException("Unexpected response");}}
                JSONObject info=new JSONObject(buffer.toString("UTF-8"));
                if(!uri.getQueryParameter("token").equals(info.optString("token"))){main.post(()->{connecting=false;status("The pairing code expired. Scan the current QR code on your PC.");});return;}
                main.post(()->{if(isFinishing())return;connecting=false;prefs.edit().putString("last_url",uri.toString()).apply();openController(uri.toString(),false);});
            }catch(Exception e){main.post(()->{connecting=false;status("Cannot reach the PC. Keep PocketPad open, check Wi-Fi and Windows Firewall, then try again.");});}
            finally{if(request!=null)request.disconnect();}
        });
    }
    @SuppressWarnings("SetJavaScriptEnabled") private void openController(String url,boolean demo){
        stopHomeMotion();homeMark=null;
        disposeBrowser();root.removeAllViews();setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_SENSOR_LANDSCAPE);
        Uri address=Uri.parse(url);origin=address.getScheme()+"://"+address.getEncodedAuthority();
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);hideBars();
        browser=new WebView(this);browser.setBackgroundColor(BG);browser.setOverScrollMode(View.OVER_SCROLL_NEVER);
        WebSettings settings=browser.getSettings();settings.setJavaScriptEnabled(true);settings.setDomStorageEnabled(true);settings.setAllowFileAccess(false);settings.setAllowContentAccess(false);settings.setMediaPlaybackRequiresUserGesture(true);settings.setSupportMultipleWindows(false);settings.setUserAgentString(settings.getUserAgentString()+" PocketPadAndroid/1.3");
        browser.addJavascriptInterface(new NativeBridge(),"PocketPadNative");
        browser.setWebChromeClient(new WebChromeClient());
        browser.setWebViewClient(new WebViewClient(){
            @Override public boolean shouldOverrideUrlLoading(WebView view,WebResourceRequest request){
                Uri uri=request.getUrl();String requested=uri.getScheme()+"://"+uri.getEncodedAuthority();
                if(request.isForMainFrame()&&"/".equals(uri.getPath())){showHome();return true;}
                return !requested.equals(origin);
            }
            @Override public WebResourceResponse shouldInterceptRequest(WebView view,WebResourceRequest request){
                Uri uri=request.getUrl();String requested=uri.getScheme()+"://"+uri.getEncodedAuthority();
                if(!requested.equals(origin))return response("text/plain",new ByteArrayInputStream(new byte[0]));
                if(demo){
                    String path=uri.getPath();String asset="play".equals(path)||"/play".equals(path)?"controller/index.html":path.startsWith("/assets/")?"controller/"+path.substring(8):"";
                    if(asset.contains("..")||asset.isEmpty())return response("text/plain",new ByteArrayInputStream(new byte[0]));
                    try{String mime=asset.endsWith(".css")?"text/css":asset.endsWith(".js")?"application/javascript":asset.endsWith(".json")?"application/json":asset.endsWith(".svg")?"image/svg+xml":"text/html";return response(mime,getAssets().open(asset));}catch(IOException e){return response("text/plain",new ByteArrayInputStream(new byte[0]));}
                }
                return null;
            }
            @Override public void onReceivedError(WebView view,WebResourceRequest request,WebResourceError error){if(request.isForMainFrame())new AlertDialog.Builder(MainActivity.this).setTitle("Connection unavailable").setMessage("Keep PocketPad open on your PC and check the network.").setPositiveButton("Retry",(d,w)->view.reload()).setNegativeButton("Home",(d,w)->showHome()).show();}
            @Override public void onPageFinished(WebView view,String finished){registerMotion();}
        });
        root.addView(browser,new FrameLayout.LayoutParams(-1,-1));browser.loadUrl(url);
    }
    private WebResourceResponse response(String mime,InputStream stream){return new WebResourceResponse(mime,"UTF-8",stream);}
    private void hideBars(){
        if(Build.VERSION.SDK_INT>=30){WindowInsetsController controller=getWindow().getInsetsController();if(controller!=null){controller.hide(WindowInsets.Type.systemBars());controller.setSystemBarsBehavior(WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);}}
        else getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_FULLSCREEN|View.SYSTEM_UI_FLAG_HIDE_NAVIGATION|View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY|View.SYSTEM_UI_FLAG_LAYOUT_STABLE);
    }
    private void haptic(){
        if(!prefs.getBoolean("haptics",true)||SystemClock.uptimeMillis()-lastHaptic<30)return;
        lastHaptic=SystemClock.uptimeMillis();Vibrator vibrator=(Vibrator)getSystemService(VIBRATOR_SERVICE);
        if(vibrator!=null&&vibrator.hasVibrator())vibrator.vibrate(VibrationEffect.createOneShot(12,VibrationEffect.DEFAULT_AMPLITUDE));
    }
    private void palette(){
        String theme=prefs.getString("appearance","matte");
        BG=theme.equals("oled")?0xff000000:theme.equals("glass")?0xff101523:0xff111216;
        PANEL=theme.equals("oled")?0xff090d0e:theme.equals("glass")?0xb8303b55:0xff191c23;
        ACCENT=theme.equals("oled")?0xff7fe3cf:theme.equals("glass")?0xffc3b3ff:0xffa7adff;
        TEXT=theme.equals("oled")?0xffe7f7f3:0xffededf5;MUTED=theme.equals("oled")?0xff7e9a92:0xff939db7;
        getWindow().setStatusBarColor(BG);getWindow().setNavigationBarColor(BG);
    }
    private void stopHomeMotion(){if(homeMotion!=null){homeMotion.cancel();homeMotion=null;}if(homeMark!=null){homeMark.setScaleX(1);homeMark.setScaleY(1);homeMark.setRotation(0);}}
    private void animateHomeMark(){
        stopHomeMotion();if(homeMark==null||!prefs.getBoolean("animations",true))return;
        ObjectAnimator x=ObjectAnimator.ofFloat(homeMark,View.SCALE_X,1,1.08f,1),y=ObjectAnimator.ofFloat(homeMark,View.SCALE_Y,1,1.08f,1),r=ObjectAnimator.ofFloat(homeMark,View.ROTATION,-3,3,-3);
        for(ObjectAnimator a:new ObjectAnimator[]{x,y,r}){a.setDuration(4400);a.setRepeatCount(ValueAnimator.INFINITE);a.setInterpolator(new android.view.animation.AccelerateDecelerateInterpolator());}
        homeMotion=new AnimatorSet();homeMotion.playTogether(x,y,r);homeMotion.start();
    }
    private String experiencePreferences(){
        try{return new JSONObject().put("appearance",prefs.getString("appearance","matte")).put("animations",prefs.getBoolean("animations",true)).put("haptics",prefs.getBoolean("haptics",true)).toString();}catch(Exception e){return "{}";}
    }
    private void publishPreferences(){palette();if(browser!=null)browser.evaluateJavascript("window.dispatchEvent(new CustomEvent('pocketpad-preferences',{detail:"+experiencePreferences()+"}))",null);if(!prefs.getBoolean("animations",true)){stopHomeMotion();cancelMotion(root);}}
    private void cancelMotion(View v){v.animate().cancel();v.clearAnimation();v.setAlpha(1);v.setTranslationX(0);v.setTranslationY(0);if(v instanceof ViewGroup){ViewGroup g=(ViewGroup)v;for(int i=0;i<g.getChildCount();i++)cancelMotion(g.getChildAt(i));}}
    private void connectionHaptic(String state){
        if(!prefs.getBoolean("haptics",true))return;
        Vibrator vibrator=(Vibrator)getSystemService(VIBRATOR_SERVICE);if(vibrator==null||!vibrator.hasVibrator())return;
        long[] pattern=state.equals("ready")?new long[]{0,16,35,16}:state.equals("reconnecting")?new long[]{0,12,55,12}:new long[]{0,10};
        vibrator.vibrate(VibrationEffect.createWaveform(pattern,-1));
    }
    private void showSettings(){
        if(browser!=null)browser.evaluateJavascript("window.dispatchEvent(new Event('pocketpad-pause'))",null);
        LinearLayout panel=new LinearLayout(this);panel.setOrientation(LinearLayout.VERTICAL);panel.setPadding(dp(24),dp(12),dp(24),dp(12));
        panel.addView(text("APPEARANCE",10,MUTED));Spinner theme=new Spinner(this);String[] choices={"Matte black","OLED black","Translucent"};String[] keys={"matte","oled","glass"};
        ArrayAdapter<String> adapter=new ArrayAdapter<>(this,android.R.layout.simple_spinner_dropdown_item,choices);theme.setAdapter(adapter);theme.setSelection(ArraysIndex(keys,prefs.getString("appearance","matte")));add(panel,theme,50,8);
        theme.setOnItemSelectedListener(new AdapterView.OnItemSelectedListener(){public void onNothingSelected(AdapterView<?> p){}public void onItemSelected(AdapterView<?> p,View v,int position,long id){prefs.edit().putString("appearance",keys[position]).apply();publishPreferences();}});
        Switch motion=new Switch(this);motion.setText("Rich animations");motion.setTextColor(TEXT);motion.setThumbTintList(android.content.res.ColorStateList.valueOf(ACCENT));motion.setChecked(prefs.getBoolean("animations",true));motion.setOnCheckedChangeListener((v,on)->{prefs.edit().putBoolean("animations",on).apply();publishPreferences();});add(panel,motion,48,12);
        TextView hint=text("On by default. Switch off for instant screens with no animation.",11,MUTED);add(panel,hint,-2,4);
        Switch feedback=new Switch(this);feedback.setText("Touch and connection haptics");feedback.setTextColor(TEXT);feedback.setThumbTintList(android.content.res.ColorStateList.valueOf(ACCENT));feedback.setChecked(prefs.getBoolean("haptics",true));feedback.setOnCheckedChangeListener((v,on)->{prefs.edit().putBoolean("haptics",on).apply();publishPreferences();});add(panel,feedback,48,12);
        AlertDialog dialog=new AlertDialog.Builder(this).setTitle("Settings").setView(panel).setPositiveButton("Done",null).setNeutralButton("Replay guide",(d,w)->openController("https://pocketpad.local/play?demo=1&tour=1",true)).create();
        dialog.setOnDismissListener(d->{if(browser==null)showHome();});dialog.show();dialog.getWindow().setBackgroundDrawable(box(BG,0xff353d52,16));
    }
    private int ArraysIndex(String[] values,String value){for(int i=0;i<values.length;i++)if(values[i].equals(value))return i;return 0;}
    private void registerMotion(){sensors.unregisterListener(this);yawRate=0;pitchRate=0;lastGyro=0;lastMotion=0;if(active&&browser!=null&&!motionMode.equals("off")){if(motionSensor!=null)sensors.registerListener(this,motionSensor,SensorManager.SENSOR_DELAY_GAME);if(gyroSensor!=null)sensors.registerListener(this,gyroSensor,SensorManager.SENSOR_DELAY_GAME);}}
    private void endPracticeMotion(){if(practiceMotionBefore!=null){motionMode=practiceMotionBefore;practiceMotionBefore=null;centered=false;registerMotion();}}
    @Override public void onSensorChanged(SensorEvent event){
        if(browser==null||motionMode.equals("off"))return;
        long now=SystemClock.uptimeMillis();
        int display=getWindowManager().getDefaultDisplay().getRotation();
        if(event.sensor.getType()==Sensor.TYPE_GYROSCOPE){
            float gx=event.values[0],gy=event.values[1];
            pitchRate=gx;yawRate=gy;
            if(display==Surface.ROTATION_90){pitchRate=gy;yawRate=-gx;}
            else if(display==Surface.ROTATION_270){pitchRate=-gy;yawRate=gx;}
            else if(display==Surface.ROTATION_180){pitchRate=-gx;yawRate=-gy;}
            lastGyro=now;
            if(motionSensor!=null)return;
        }
        if(now-lastMotion<20)return;lastMotion=now;
        if(event.sensor.getType()!=Sensor.TYPE_GYROSCOPE){
            SensorManager.getRotationMatrixFromVector(rotation,event.values);
            int x=SensorManager.AXIS_X,y=SensorManager.AXIS_Y;
            if(display==Surface.ROTATION_90){x=SensorManager.AXIS_Y;y=SensorManager.AXIS_MINUS_X;}
            else if(display==Surface.ROTATION_270){x=SensorManager.AXIS_MINUS_Y;y=SensorManager.AXIS_X;}
            else if(display==Surface.ROTATION_180){x=SensorManager.AXIS_MINUS_X;y=SensorManager.AXIS_MINUS_Y;}
            SensorManager.remapCoordinateSystem(rotation,x,y,remapped);SensorManager.getOrientation(remapped,orientation);
            currentRoll=(float)Math.toDegrees(orientation[2]);currentPitch=(float)Math.toDegrees(orientation[1]);
            if(!centered){centerRoll=currentRoll;centerPitch=currentPitch;centered=true;}
        }
        float roll=currentRoll-centerRoll,pitch=currentPitch-centerPitch;
        while(roll>180)roll-=360;while(roll< -180)roll+=360;
        String rates=gyroSensor==null?"":String.format(Locale.US,",yawRate:%.5f,pitchRate:%.5f",now-lastGyro<150?yawRate:0,now-lastGyro<150?pitchRate:0);
        String script=String.format(Locale.US,"window.dispatchEvent(new CustomEvent('pocketpad-motion',{detail:{roll:%.4f,pitch:%.4f%s}}))",roll,pitch,rates);
        browser.evaluateJavascript(script,null);
    }
    @Override public void onAccuracyChanged(Sensor sensor,int accuracy){}
    private final class NativeBridge {
        @JavascriptInterface public String getPreferences(){return experiencePreferences();}
        @JavascriptInterface public boolean tourCompleted(){return prefs.getBoolean("tourComplete",false);}
        @JavascriptInterface public void completeTour(){prefs.edit().putBoolean("tourComplete",true).apply();}
        @JavascriptInterface public void connectionCue(String state){if(!state.equals("ready")&&!state.equals("connecting")&&!state.equals("reconnecting"))return;main.post(()->connectionHaptic(state));}
        @JavascriptInterface public void beginPracticeMotion(String mode){if(!mode.equals("steer")&&!mode.equals("aim"))return;main.post(()->{if(practiceMotionBefore==null)practiceMotionBefore=motionMode;motionMode=mode;centered=false;registerMotion();});}
        @JavascriptInterface public void endPracticeMotion(){main.post(MainActivity.this::endPracticeMotion);}
        @JavascriptInterface public void settings(){main.post(MainActivity.this::showSettings);}
        @JavascriptInterface public void savePreferences(String json){main.post(()->{try{JSONObject value=new JSONObject(json);String theme=value.optString("appearance","matte");if(!theme.equals("matte")&&!theme.equals("oled")&&!theme.equals("glass"))theme="matte";prefs.edit().putString("appearance",theme).putBoolean("animations",value.optBoolean("animations",true)).putBoolean("haptics",value.optBoolean("haptics",true)).apply();publishPreferences();}catch(Exception ignored){}});}
        @JavascriptInterface public void clickHaptic(){main.post(MainActivity.this::haptic);}
        @JavascriptInterface public boolean hasMotion(){return motionSensor!=null||gyroSensor!=null;}
        @JavascriptInterface public String getMotionPreferences(){return prefs.getString("gyroSettings","{}");}
        @JavascriptInterface public void saveMotionPreferences(String json){if(json.length()>1024)return;try{new JSONObject(json);prefs.edit().putString("gyroSettings",json).apply();}catch(Exception ignored){}}
        @JavascriptInterface public String getMotionMode(){return motionMode;}
        @JavascriptInterface public void setMotion(String mode){if(!mode.equals("off")&&!mode.equals("steer")&&!mode.equals("aim"))return;main.post(()->{practiceMotionBefore=null;motionMode=mode;centered=false;prefs.edit().putString("motion",mode).apply();registerMotion();});}
        @JavascriptInterface public void recenter(){main.post(()->centered=false);}
        @JavascriptInterface public void fullscreen(){main.post(MainActivity.this::hideBars);}
        @JavascriptInterface public void setInputMode(String mode){
            if(!mode.equals("desktop")&&!mode.equals("gamepad"))return;
            main.post(()->setRequestedOrientation(mode.equals("desktop")?ActivityInfo.SCREEN_ORIENTATION_FULL_SENSOR:ActivityInfo.SCREEN_ORIENTATION_SENSOR_LANDSCAPE));
        }
        @JavascriptInterface public void showKeyboard(){main.post(()->{if(browser==null)return;browser.requestFocus();((InputMethodManager)getSystemService(INPUT_METHOD_SERVICE)).showSoftInput(browser,InputMethodManager.SHOW_IMPLICIT);});}
        @JavascriptInterface public void hideKeyboard(){main.post(()->{if(browser!=null)((InputMethodManager)getSystemService(INPUT_METHOD_SERVICE)).hideSoftInputFromWindow(browser.getWindowToken(),0);});}
        @JavascriptInterface public void home(){main.post(MainActivity.this::showHome);}
    }
    @Override protected void onResume(){super.onResume();active=true;centered=false;if(browser!=null){browser.onResume();getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);hideBars();registerMotion();}else animateHomeMark();}
    @Override protected void onPause(){active=false;endPracticeMotion();stopHomeMotion();sensors.unregisterListener(this);if(browser!=null){browser.evaluateJavascript("window.dispatchEvent(new Event('pocketpad-pause'))",null);browser.onPause();}super.onPause();}
    @Override public void onBackPressed(){if(browser!=null){browser.evaluateJavascript("window.dispatchEvent(new Event('pocketpad-pause'))",null);new AlertDialog.Builder(this).setTitle("Leave controller?").setMessage("Controls are paused.").setNegativeButton("Stay",(d,w)->{}).setPositiveButton("Home",(d,w)->showHome()).show();}else super.onBackPressed();}
    @Override protected void onDestroy(){disposeBrowser();network.shutdownNow();super.onDestroy();}
}
