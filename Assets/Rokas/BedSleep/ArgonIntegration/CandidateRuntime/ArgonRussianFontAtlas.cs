using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    /// <summary>
    /// Portable, antialiased Noto Sans Cyrillic glyph raster subset baked as
    /// compact alpha masks. NO platform font dependency, no original font
    /// binaries distributed. Every runtime label uses actual Cyrillic.
    /// </summary>
    internal static class ArgonRussianFontAtlas
    {
        private const string Alphabet = "0123456789:+-. АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ";
        private const int CellW=24, CellH=28;
        private const int PackedGlyphBytes=CellW*CellH/2;
        private const string MaskGzipBase64 = "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";
        private static byte[] data;
        private static int[] widths;

        private static void EnsureLoaded()
        {
            if(data!=null)return;
            byte[] encoded=Convert.FromBase64String(MaskGzipBase64);
            using(var source=new MemoryStream(encoded))
            using(var gz=new GZipStream(source,CompressionMode.Decompress))
            using(var buffer=new MemoryStream())
            {
                gz.CopyTo(buffer);
                byte[] decoded=buffer.ToArray();
                if(decoded.Length!=Alphabet.Length*(PackedGlyphBytes+1))
                    throw new InvalidDataException("Corrupt embedded Cyrillic glyph atlas");
                widths=new int[Alphabet.Length];
                for(int i=0;i<widths.Length;i++) widths[i]=decoded[i];
                data=new byte[decoded.Length-Alphabet.Length];
                Buffer.BlockCopy(decoded,Alphabet.Length,data,0,data.Length);
            }
        }
        private static float Sample(int gi,int x,int y)
        {
            if(x<0||y<0||x>=CellW||y>=CellH)return 0f;
            int offset=gi*PackedGlyphBytes+(y*CellW+x)/2;
            byte b=data[offset];
            int value=((x+y*CellW)%2==0)?((b>>4)&15):(b&15);
            return value/15f;
        }
        private static float Bilinear(int gi,float x,float y)
        {
            int x0=Mathf.FloorToInt(x),y0=Mathf.FloorToInt(y);
            float fx=x-x0,fy=y-y0;
            float a=Mathf.Lerp(Sample(gi,x0,y0),Sample(gi,x0+1,y0),fx);
            float b=Mathf.Lerp(Sample(gi,x0,y0+1),Sample(gi,x0+1,y0+1),fx);
            return Mathf.Lerp(a,b,fy);
        }
        public static void Draw(string text,int centerX,int centerY,
            float scale,Color32 color,Action<int,int,Color32,float> pixel)
        {
            EnsureLoaded();
            if(string.IsNullOrEmpty(text))return;
            text=text.ToUpperInvariant();
            float len=0f;
            for(int i=0;i<text.Length;i++)
            {
                int gi=Alphabet.IndexOf(text[i]);
                len+=(gi>=0?widths[gi]+2:10)*scale;
            }
            float left=centerX-len*.5f;
            foreach(char ch in text)
            {
                int gi=Alphabet.IndexOf(ch);
                if(gi<0){left+=10*scale;continue;}
                int w=widths[gi]+2;
                int x0=Mathf.FloorToInt(left);
                int x1=Mathf.CeilToInt(left+(w+1)*scale);
                int y0=Mathf.FloorToInt(centerY-CellH*scale*.5f);
                int y1=Mathf.CeilToInt(centerY+CellH*scale*.5f);
                for(int y=y0;y<=y1;y++)
                {
                    float srcY=(centerY-y)/scale+CellH*.5f;
                    for(int x=x0;x<=x1;x++)
                    {
                        float srcX=(x-left)/scale;
                        float alpha=Bilinear(gi,srcX,srcY);
                        if(alpha>.012f)pixel(x,y,color,alpha);
                    }
                }
                left+=w*scale;
            }
        }
    }
}
