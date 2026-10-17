using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class DrawingPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    internal DrawingPoint Copy() { return new DrawingPoint { X=X,Y=Y }; }
}

internal static class DrawingTrajectoryConverter
{
    internal const int MaxDrawingPoints=64;
    private sealed class Vertex { internal int Time; internal double X,Y; }

    internal static Trajectory Convert(IList<DrawingPoint> points,int durationMs,double scale,bool invert,bool smooth,bool step=false)
    {
        if(points==null||points.Count<2||points.Count>MaxDrawingPoints)
            throw new ArgumentException("Desenhe de 2 a 64 pontos antes de aplicar.");
        int minimum=step?1:2;
        if(durationMs<Math.Max(100,(points.Count-1)*minimum)||durationMs>Trajectory.MaxTime)
            throw new ArgumentException("Use uma duração de 100 a 60000 ms e pelo menos 2 ms por trecho gradual.");
        if(double.IsNaN(scale)||double.IsInfinity(scale)||scale<0.01||scale>10)
            throw new ArgumentException("Use uma escala entre 0,01 e 10 px por unidade.");
        if(points.Any(p=>p==null||!Finite(p.X)||!Finite(p.Y)||p.X<0||p.X>600||p.Y<0||p.Y>360))
            throw new ArgumentException("Há pontos fora da área de desenho.");
        var distances=new List<double>(); double total=0;
        for(int i=1;i<points.Count;i++) { double dx=points[i].X-points[i-1].X,dy=points[i].Y-points[i-1].Y; double d=Math.Sqrt(dx*dx+dy*dy); distances.Add(d); total+=d; }
        if(total<0.01)throw new ArgumentException("O desenho precisa ter algum deslocamento.");
        double distance=0,sign=invert?-1:1; var vertices=new List<Vertex> { new Vertex() };
        for(int i=1;i<points.Count;i++)
        {
            distance+=distances[i-1]; int remaining=points.Count-1-i, start=vertices[i-1].Time;
            int end=i==points.Count-1?durationMs:Math.Max(start+minimum,Math.Min(durationMs-remaining*minimum,(int)Math.Round(durationMs*distance/total)));
            vertices.Add(new Vertex { Time=end,X=sign*(points[i].X-points[0].X)*scale,Y=sign*(points[i].Y-points[0].Y)*scale });
        }
        return Build(vertices,smooth,step);
    }

    internal static bool CanSmoothLegacy(Trajectory source)
    {
        return source!=null&&!source.Step&&source.Points.Count>=3&&source.Points.Count%2==1&&
            source.Points.Where((p,i)=>i%2==0).All(p=>p.Horizontal==0&&p.Vertical==0);
    }
    // Explicit, undoable repair for the zero/peak/zero drawings produced by earlier builds.
    // Spatial endpoints and their times are reconstructed from the original exact integral.
    internal static Trajectory SmoothLegacy(Trajectory source)
    {
        if(!CanSmoothLegacy(source))throw new ArgumentException("Não há o padrão antigo de paradas alternadas neste desenho. Para um desenho novo, selecione Suave antes de aplicar.");
        var vertices=new List<Vertex>();
        for(int i=0;i<source.Points.Count;i+=2)
        {
            var displacement=source.Integrate(0,source.Points[i].TimeMs);
            vertices.Add(new Vertex { Time=source.Points[i].TimeMs,X=displacement.Horizontal,Y=displacement.Vertical });
        }
        var result=Build(vertices,true,false); result.Enabled=source.Enabled; result.HoldLast=source.HoldLast; return result;
    }
    private static Trajectory Build(IList<Vertex> vertices,bool smooth,bool step)
    {
        int count=vertices.Count; var slopeX=new double[count-1]; var slopeY=new double[count-1]; var duration=new double[count-1];
        for(int i=0;i<count-1;i++)
        {
            duration[i]=(vertices[i+1].Time-vertices[i].Time)/1000.0;
            if(duration[i]<=0||(!step&&vertices[i+1].Time-vertices[i].Time<2))throw new ArgumentException("Aumente o tempo entre os pontos para suavizar este desenho.");
            slopeX[i]=(vertices[i+1].X-vertices[i].X)/duration[i]; slopeY[i]=(vertices[i+1].Y-vertices[i].Y)/duration[i];
        }
        var result=new Trajectory { Enabled=true,Step=step,Smooth=smooth,Points=new List<TrajectoryPoint>() };
        if(step)
        {
            for(int i=0;i<count-1;i++)result.Points.Add(Point(vertices[i].Time,slopeX[i],slopeY[i]));
            result.Points.Add(Point(vertices[count-1].Time,0,0)); return result;
        }
        var tangentX=new double[count]; var tangentY=new double[count];
        tangentX[0]=slopeX[0]; tangentY[0]=slopeY[0];
        for(int i=1;i<count-1;i++)
        {
            double before=Math.Sqrt(slopeX[i-1]*slopeX[i-1]+slopeY[i-1]*slopeY[i-1]);
            double after=Math.Sqrt(slopeX[i]*slopeX[i]+slopeY[i]*slopeY[i]);
            if(before==0||after==0)continue; // Preserve explicitly stationary intervals.
            double w1=2*duration[i]+duration[i-1],w2=duration[i]+2*duration[i-1];
            double speed=Math.Min((w1+w2)/(w1/before+w2/after),2*Math.Min(before,after));
            // Blend directions as vectors: a right-angle turn must not zero both axes.
            // Exact reversals can still have an instantaneous zero, as any continuous velocity must.
            tangentX[i]=0.5*speed*(slopeX[i-1]/before+slopeX[i]/after);
            tangentY[i]=0.5*speed*(slopeY[i-1]/before+slopeY[i]/after);
        }
        // The last velocity is zero for the explicit final stop. Interior vertices keep their tangent.
        result.Points.Add(Point(vertices[0].Time,tangentX[0],tangentY[0]));
        for(int i=0;i<count-1;i++)
        {
            int middle=vertices[i].Time+(vertices[i+1].Time-vertices[i].Time)/2;
            double left=(middle-vertices[i].Time)/1000.0,right=(vertices[i+1].Time-middle)/1000.0;
            // Both linear and smoothstep velocity average their endpoints. This midpoint correction
            // preserves the exact drawn displacement without forcing a zero at every vertex.
            double vx=(2*(vertices[i+1].X-vertices[i].X)-tangentX[i]*left-tangentX[i+1]*right)/duration[i];
            double vy=(2*(vertices[i+1].Y-vertices[i].Y)-tangentY[i]*left-tangentY[i+1]*right)/duration[i];
            result.Points.Add(Point(middle,vx,vy));
            result.Points.Add(Point(vertices[i+1].Time,tangentX[i+1],tangentY[i+1]));
        }
        return result;
    }
    private static TrajectoryPoint Point(int time,double x,double y)
    {
        if(!Finite(x)||!Finite(y)||Math.Abs(x)>5000||Math.Abs(y)>10000)
            throw new ArgumentException("O desenho excede a velocidade permitida. Aumente a duração ou reduza a escala.");
        return new TrajectoryPoint { TimeMs=time,Horizontal=x,Vertical=y };
    }
    private static bool Finite(double number) { return !double.IsNaN(number)&&!double.IsInfinity(number); }
}
