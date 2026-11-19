package io.pocketpad.app;

import android.animation.ValueAnimator;
import android.content.Context;
import android.graphics.*;
import android.view.View;
import android.view.animation.DecelerateInterpolator;

/** A short, drawn-on controller mark. No video, network, or bitmap dependencies. */
public final class LaunchView extends View {
    private final Paint paint=new Paint(3);
    private final Path outline=new Path();
    private final Path segment=new Path();
    private float progress=0;
    private ValueAnimator animation;
    private final int accent;
    public LaunchView(Context context){this(context,0xff111216,0xffa7adff);}
    public LaunchView(Context context,int background,int tone){super(context);accent=tone;setBackgroundColor(background);}
    public void play(Runnable done){
        animation=ValueAnimator.ofFloat(0,1);
        animation.setDuration(1450);
        animation.setInterpolator(new DecelerateInterpolator());
        animation.addUpdateListener(a->{progress=(float)a.getAnimatedValue();invalidate();});
        animation.addListener(new android.animation.AnimatorListenerAdapter(){
            @Override public void onAnimationEnd(android.animation.Animator a){postDelayed(done,100);}
        });
        animation.start();
    }
    @Override protected void onDetachedFromWindow(){if(animation!=null)animation.removeAllListeners();super.onDetachedFromWindow();}
    @Override protected void onDraw(Canvas canvas){
        super.onDraw(canvas);
        float density=getResources().getDisplayMetrics().density;
        float cx=getWidth()/2f,cy=getHeight()/2f-38*density;
        float s=density*1.65f;
        paint.setStyle(Paint.Style.STROKE);paint.setStrokeWidth(density);paint.setColor(accent);
        for(int i=0;i<3;i++){paint.setAlpha((int)(50*(1-progress)*(1-i*.2)));canvas.drawCircle(cx,cy+2*density,(48+progress*45+i*17)*density,paint);}
        canvas.save();canvas.translate(cx-54*s,cy-54*s);canvas.scale(s,s);
        outline.reset();outline.moveTo(36,37);outline.lineTo(72,37);outline.quadTo(80,37,83,48);outline.lineTo(88,65);outline.quadTo(91,78,83,78);outline.quadTo(79,78,70,67);outline.lineTo(38,67);outline.quadTo(29,78,25,78);outline.quadTo(17,78,20,65);outline.lineTo(25,48);outline.quadTo(28,37,36,37);outline.close();
        segment.reset();PathMeasure measure=new PathMeasure(outline,false);measure.getSegment(0,measure.getLength()*Math.min(1,progress*1.4f),segment,true);
        paint.setStyle(Paint.Style.STROKE);paint.setStrokeWidth(3.2f);paint.setStrokeCap(Paint.Cap.ROUND);paint.setStrokeJoin(Paint.Join.ROUND);paint.setColor(0xffededf2);canvas.drawPath(segment,paint);
        paint.setAlpha((int)(255*Math.max(0,(progress-.18f)/.82f)));
        canvas.drawLine(37,47,37,59,paint);canvas.drawLine(31,53,43,53,paint);
        paint.setStyle(Paint.Style.FILL);canvas.drawCircle(64,57,2.7f*progress,paint);canvas.drawCircle(79,57,2.7f*progress,paint);
        paint.setColor(accent);canvas.drawCircle(72,49,3.2f*progress,paint);
        canvas.restore();
        paint.setColor(0xffededf2);paint.setAlpha((int)(255*Math.max(0,(progress-.35f)/.65f)));paint.setTextAlign(Paint.Align.CENTER);paint.setTypeface(Typeface.create("sans-serif-medium",Typeface.NORMAL));paint.setTextSize(27*density);canvas.drawText("PocketPad",cx,cy+82*density,paint);
        paint.setColor(0xff74747f);paint.setAlpha((int)(255*Math.max(0,(progress-.55f)/.45f)));paint.setTextSize(10*density);paint.setLetterSpacing(0);canvas.drawText("ANDROID COMPANION",cx,cy+106*density,paint);
    }
}
