using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using UnityEngine.UI;

public class SegmentDetection : MonoBehaviour
{

    [Tooltip("The image of the UI showing the segments.")]
    public RawImage rawImage;
    // Used for capturing the image of the car camera
    public RenderTexture carcamera;

    // Pour l'appel a la bibliothèque en C à partir de C#
    // https://www.mono-project.com/docs/advanced/pinvoke/#marshaling
    // Bibliothèque: Line Segment Detector de R. Grompone von Gioi, J. Jakubowicz, J.-M. Morel, G. Randall 
    // http://www.ipol.im/pub/art/2012/gjmr-lsd/?utm_source=doi
    private const int SIZEPREVIEW = 128;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = SIZEPREVIEW*SIZEPREVIEW)]
    // The pixel array for the image captured
    private int[] pixels;
    /*
    private const int MAXSEGMENTS = 10; // il y a 10 segments au plus. Ne pas changer sans changer le nombre au dessous
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10*8*MAXSEGMENTS)] // 10 segments = 10*8*7 octets

    public double[] segments;
    [Tooltip("When different from 1.0, LSD will scale the input image by 'scale' factor by Gaussian filtering, before detecting line segments.")]
    [Range(0, 10)]
    public double scale = 0.8;
    [Tooltip("When scale!=1.0, the sigma of the Gaussian filter is: sigma = sigma_scale / scale, if scale < 1.0; sigma = sigma_scale, if scale >= 1.0")]
    [Range(0, 10)]
    public double sigma_scale = 0.6;
    [Tooltip("Bound to the quantization error on the gradient norm.")]
    [Range(0, 10)]
    public double quant = 2;
    [Range(0, 180)]
    [Tooltip("Gradient angle tolerance in the region growing algorithm, in degrees.")]
    public double ang_th = 22.5;
    [Tooltip("Detection threshold, accept if -log10(NFA) > log_eps.The larger the value, the more strict the detector is, and will result in less detections.")]
    [Range(-10, 10)]
    public double log_eps = 0;
    [Tooltip("Minimal proportion of 'supporting' points in a rectangle.")]
    [Range(0, 1)]
    public double density_th = 0.7;
    [Tooltip("Number of bins used in the pseudo-ordering of gradient modulus.")]
    [Range(1, 4096)]
    public int n_bins = 1024;

    [DllImport("lsd")]
    private static extern void lsd_with_param([In, Out] ref int n, [In, Out]double[] segments, double[] pixels, int x, int y, double scale, double sigma_scale, double quant, double ang_th, double log_eps, double density_th, int n_bins);
    */
    private Color[] segpixels;
    private Texture2D segTex,tex;

    // Start is called before the first frame update
    void Start()
    {
        /*segments = new double[7 * MAXSEGMENTS];*/
        /*segpixels = new Color[SIZEPREVIEW * SIZEPREVIEW];*/
        /*segTex = new Texture2D(SIZEPREVIEW, SIZEPREVIEW);*/
        tex = new Texture2D(SIZEPREVIEW, SIZEPREVIEW);
        pixels = new int[SIZEPREVIEW * SIZEPREVIEW];
    }

    // Update is called once per frame
    void Update()
    {
       /* int n = MAXSEGMENTS;*/
        RenderTexture.active = carcamera;
        tex.ReadPixels(new Rect(0, 0, SIZEPREVIEW, SIZEPREVIEW), 0, 0);
        tex.Apply();
        Color32[] pix = tex.GetPixels32();
        for (int i = 0; i < SIZEPREVIEW * SIZEPREVIEW; i++)
            pixels[i] = pix[i].r;
        //for (int i = 0; i < SIZEPREVIEW * SIZEPREVIEW; i++) segpixels[i] = Color.white;
        int left = -1, right = SIZEPREVIEW;
        for (int i=0;i<SIZEPREVIEW;i++)
        {
            if (pixels[SIZEPREVIEW*SIZEPREVIEW/2+i]<100)
            {
                if (left < 0&&i<SIZEPREVIEW/2) left = i;
                else if (right >= SIZEPREVIEW&&i>=SIZEPREVIEW/2) right = i;
            }
        }
        if (left<17) 
        Debug.Log("left:" + left+" right:"+right);
        //lsd_with_param(ref n, segments, pixels, SIZEPREVIEW, SIZEPREVIEW, scale, sigma_scale, quant, ang_th, log_eps, density_th, n_bins);
        /*for (int i = 0; i < n; i++)
        {
            DrawLine((int)segments[i * 7 + 1], (int)segments[i * 7 + 0], (int)segments[i * 7 + 3], (int)segments[i * 7 + 2], Color.red);
        }*/
        /*segTex.SetPixels(segpixels);
        segTex.Apply();
        rawImage.texture = segTex;*/
    }

    // Bresenham line algorithm, for drawing segments
    private void DrawLine(int x0, int y0, int x1, int y1, Color c)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            if (x0 < 128 && x0 >= 0 && y0 < 128 && y0 >= 0)
                segpixels[x0 * 128 + y0] = c;

            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }
}
