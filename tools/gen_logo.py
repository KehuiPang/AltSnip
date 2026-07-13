from PIL import Image, ImageDraw
import sys

INK   = (0x16,0x19,0x1E)
INK2  = (0x22,0x38,0x4A)
MOON  = (0xF4,0xF6,0xF8)
SILVER= (0xAE,0xB8,0xC0)
SPARK = (0xC0,0x5F,0x3C)

def lerp(a,b,t): return tuple(int(round(a[i]+(b[i]-a[i])*t)) for i in range(3))

def diag_gradient(size, c0, c1):
    """linear gradient along (0,0)->(S,S)"""
    S=size
    img=Image.new("RGB",(S,S))
    px=img.load()
    for y in range(S):
        for x in range(S):
            t=(x+y)/(2*(S-1))
            px[x,y]=lerp(c0,c1,t)
    return img

def draw_logo(S):
    SS=4  # supersample
    B=S*SS
    # background gradient masked by rounded rect
    bg=diag_gradient(B, INK, INK2).convert("RGBA")
    mask=Image.new("L",(B,B),0)
    md=ImageDraw.Draw(mask)
    radius=int(round(58*B/256))
    md.rounded_rectangle([0,0,B-1,B-1], radius=radius, fill=255)
    canvas=Image.new("RGBA",(B,B),(0,0,0,0))
    canvas.paste(bg,(0,0),mask)

    # ---- glow (radial, spark alpha 130->0 over r=dotR*2.8) ----
    cx=cy=B/2
    dotR=B*0.056
    R=dotR*2.8
    glow=Image.new("RGBA",(B,B),(0,0,0,0))
    gp=glow.load()
    import math
    x0=int(cx-R)-1; x1=int(cx+R)+1; y0=int(cy-R)-1; y1=int(cy+R)+1
    for y in range(max(0,y0),min(B,y1)):
        for x in range(max(0,x0),min(B,x1)):
            d=math.hypot(x-cx,y-cy)
            if d<R:
                a=int(130*(1-d/R))
                gp[x,y]=(SPARK[0],SPARK[1],SPARK[2],a)
    canvas=Image.alpha_composite(canvas,glow)

    # ---- corner brackets (gradient MOON->SILVER), rounded caps ----
    d=ImageDraw.Draw(canvas)
    sw=B*0.072
    inset=B*0.265; arm=B*0.155
    lo=inset; hi=B-inset
    grad=diag_gradient(B,MOON,SILVER).convert("RGBA")
    bmask=Image.new("L",(B,B),0)
    bd=ImageDraw.Draw(bmask)
    r=sw/2
    def cap(x,y):
        bd.ellipse([x-r,y-r,x+r,y+r],fill=255)
    def seg(x0,y0,x1,y1):
        bd.line([x0,y0,x1,y1],fill=255,width=int(round(sw)))
        cap(x0,y0); cap(x1,y1)
    def bracket(ex,ey,dx,dy):
        seg(ex+dx*arm,ey,ex,ey)
        seg(ex,ey,ex,ey+dy*arm)
    bracket(lo,lo,+1,+1)
    bracket(hi,lo,-1,+1)
    bracket(lo,hi,+1,-1)
    bracket(hi,hi,-1,-1)
    canvas.paste(grad,(0,0),bmask)

    # ---- center spark dot ----
    dd=ImageDraw.Draw(canvas)
    dd.ellipse([cx-dotR,cy-dotR,cx+dotR,cy+dotR],fill=SPARK+(255,))

    # downsample
    return canvas.resize((S,S), Image.LANCZOS)

if __name__=="__main__":
    S=int(sys.argv[1]) if len(sys.argv)>1 else 1024
    out=sys.argv[2] if len(sys.argv)>2 else "logo_gen.png"
    draw_logo(S).save(out)
    print("wrote",out,S)
