package io.pocketpad.app;

import android.app.Activity;
import android.content.Intent;
import android.graphics.*;
import android.hardware.Camera;
import android.os.Bundle;
import android.view.*;
import android.widget.*;
import com.google.zxing.*;
import com.google.zxing.common.HybridBinarizer;
import java.util.*;
import java.util.concurrent.*;
import java.util.concurrent.atomic.AtomicBoolean;

/** Offline QR camera scanner; frames never leave the phone. */
@SuppressWarnings("deprecation") public final class ScanActivity extends Activity implements SurfaceHolder.Callback {
    private Camera camera;
    private SurfaceView preview;
    private final ExecutorService decoder=Executors.newSingleThreadExecutor();
    private final AtomicBoolean busy=new AtomicBoolean(false);
    private volatile boolean stopped=false;
    private long lastScan=0;
    private boolean torch=false;
    private int dp(int value){return Math.round(value*getResources().getDisplayMetrics().density);}
    @Override public void onCreate(Bundle saved){
        super.onCreate(saved);getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        FrameLayout root=new FrameLayout(this);root.setBackgroundColor(0xff101012);preview=new SurfaceView(this);root.addView(preview,new FrameLayout.LayoutParams(-1,-1));preview.getHolder().addCallback(this);
        root.addView(new Frame(this),new FrameLayout.LayoutParams(-1,-1));
        LinearLayout bar=new LinearLayout(this);bar.setOrientation(LinearLayout.VERTICAL);bar.setPadding(dp(24),dp(30),dp(24),dp(24));bar.setGravity(Gravity.CENTER_HORIZONTAL);
        TextView title=new TextView(this);title.setText("Scan PocketPad QR");title.setTextColor(Color.WHITE);title.setTextSize(22);bar.addView(title);
        TextView hint=new TextView(this);hint.setText("On your PC: Connection → Connect phone");hint.setTextColor(0xffbbbbc4);hint.setTextSize(12);hint.setPadding(0,dp(10),0,0);bar.addView(hint);
        FrameLayout.LayoutParams top=new FrameLayout.LayoutParams(-1,-2,Gravity.TOP);root.addView(bar,top);
        LinearLayout actions=new LinearLayout(this);actions.setGravity(Gravity.CENTER);actions.setPadding(dp(20),dp(20),dp(20),dp(30));
        Button cancel=new Button(this);cancel.setText("Cancel");cancel.setAllCaps(false);cancel.setOnClickListener(v->finish());actions.addView(cancel);
        Button light=new Button(this);light.setText("Flashlight");light.setAllCaps(false);light.setOnClickListener(v->toggleTorch());actions.addView(light);
        root.addView(actions,new FrameLayout.LayoutParams(-1,-2,Gravity.BOTTOM));setContentView(root);
    }
    private void openCamera(SurfaceHolder holder){
        if(camera!=null||stopped||!holder.getSurface().isValid())return;
        try{
            int id=0;for(int i=0;i<Camera.getNumberOfCameras();i++){Camera.CameraInfo info=new Camera.CameraInfo();Camera.getCameraInfo(i,info);if(info.facing==Camera.CameraInfo.CAMERA_FACING_BACK){id=i;break;}}
            camera=Camera.open(id);Camera.Parameters p=camera.getParameters();
            Camera.Size best=null;for(Camera.Size size:p.getSupportedPreviewSizes())if(size.width<=1280&&(best==null||size.width>best.width))best=size;
            if(best!=null)p.setPreviewSize(best.width,best.height);
            if(p.getSupportedFocusModes().contains(Camera.Parameters.FOCUS_MODE_CONTINUOUS_PICTURE))p.setFocusMode(Camera.Parameters.FOCUS_MODE_CONTINUOUS_PICTURE);
            p.setPreviewFormat(ImageFormat.NV21);camera.setParameters(p);camera.setDisplayOrientation(90);camera.setPreviewDisplay(holder);
            camera.setPreviewCallback((data,device)->{
                long now=android.os.SystemClock.uptimeMillis();if(stopped||now-lastScan<140||!busy.compareAndSet(false,true))return;lastScan=now;
                Camera.Size size=device.getParameters().getPreviewSize();byte[] frame=Arrays.copyOf(data,data.length);
                decoder.execute(()->{
                    try{
                        PlanarYUVLuminanceSource source=new PlanarYUVLuminanceSource(frame,size.width,size.height,0,0,size.width,size.height,false);
                        MultiFormatReader reader=new MultiFormatReader();Map<DecodeHintType,Object> hints=new EnumMap<>(DecodeHintType.class);hints.put(DecodeHintType.POSSIBLE_FORMATS,Collections.singletonList(BarcodeFormat.QR_CODE));hints.put(DecodeHintType.TRY_HARDER,Boolean.TRUE);reader.setHints(hints);
                        Result result=reader.decodeWithState(new BinaryBitmap(new HybridBinarizer(source)));
                        if(!stopped){stopped=true;runOnUiThread(()->{setResult(RESULT_OK,new Intent().putExtra("url",result.getText()));finish();});}
                    }catch(Exception ignored){}finally{busy.set(false);}
                });
            });camera.startPreview();
        }catch(Exception e){Toast.makeText(this,"Camera unavailable. Enter the pairing link instead.",Toast.LENGTH_LONG).show();finish();}
    }
    private void closeCamera(){if(camera!=null){try{camera.setPreviewCallback(null);camera.stopPreview();}catch(RuntimeException ignored){}finally{camera.release();camera=null;}}}
    private void toggleTorch(){if(camera==null)return;try{Camera.Parameters p=camera.getParameters();List<String> modes=p.getSupportedFlashModes();if(modes!=null&&modes.contains(Camera.Parameters.FLASH_MODE_TORCH)){torch=!torch;p.setFlashMode(torch?Camera.Parameters.FLASH_MODE_TORCH:Camera.Parameters.FLASH_MODE_OFF);camera.setParameters(p);}}catch(Exception ignored){}}
    @Override public void surfaceCreated(SurfaceHolder holder){openCamera(holder);}
    @Override public void surfaceChanged(SurfaceHolder holder,int format,int w,int h){}
    @Override public void surfaceDestroyed(SurfaceHolder holder){closeCamera();}
    @Override protected void onPause(){stopped=true;closeCamera();super.onPause();}
    @Override protected void onResume(){super.onResume();stopped=false;if(preview!=null)openCamera(preview.getHolder());}
    @Override protected void onDestroy(){stopped=true;closeCamera();decoder.shutdownNow();super.onDestroy();}
    private static final class Frame extends View {
        private final Paint p=new Paint(3);
        Frame(android.content.Context context){super(context);setLayerType(View.LAYER_TYPE_SOFTWARE,null);}
        @Override protected void onDraw(Canvas canvas){float side=Math.min(getWidth()*.72f,getHeight()*.45f),left=(getWidth()-side)/2,top=(getHeight()-side)/2;RectF rect=new RectF(left,top,left+side,top+side);p.setColor(0x88000000);canvas.drawRect(0,0,getWidth(),getHeight(),p);p.setXfermode(new PorterDuffXfermode(PorterDuff.Mode.CLEAR));canvas.drawRoundRect(rect,18,18,p);p.setXfermode(null);p.setStyle(Paint.Style.STROKE);p.setColor(0xffaaaaff);p.setStrokeWidth(3);canvas.drawRoundRect(rect,18,18,p);p.setStyle(Paint.Style.FILL);}
    }
}
