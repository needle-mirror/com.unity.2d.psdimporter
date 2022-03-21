using System.Collections.Generic;

namespace PDNWrapper
{
    internal static class Layer
    {
        public static BitmapLayer CreateBackgroundLayer(int w, int h)
        {
            return new BitmapLayer(w, h, new Rectangle(0, 0, w, h));
        }
    }

    internal class BitmapLayer
    {
        int width, height;
        List<BitmapLayer> m_ChildLayers = new List<BitmapLayer>();
        
        public Rectangle Bounds
        {
            get {return new Rectangle(0, 0, width, height); }
        }

        public void Dispose()
        {
            Surface.Dispose();
            foreach (var layer in ChildLayer)
                layer.Dispose();
        }

        public BitmapLayer(int w, int h, Rectangle rect)
        {
            Surface = new Surface(w, h);
            width = w;
            height = h;
            m_ChildLayers = new List<BitmapLayer>();
            IsGroup = false;
            this.rect = rect;
        }

        public void AddChildLayer(BitmapLayer c)
        {
            m_ChildLayers.Add(c);
            var bound = c.rect;
            foreach (var child in ChildLayer)
            {
                bound.Y = bound.Y > child.rect.Y ? child.rect.Y : bound.Y;
                bound.X = bound.X > child.rect.X ? child.rect.X : bound.X;
                bound.Width = bound.Right < child.rect.Right ? child.rect.Right - bound.X : bound.Width;
                bound.Height = bound.Bottom < child.rect.Bottom ? child.rect.Bottom - bound.Y : bound.Height;
            }

            rect = bound;
        }
        
        public int LayerID { get; set; }
        public bool IsGroup {get; set; }
        public BitmapLayer ParentLayer {get; set; }
        public IEnumerable<BitmapLayer> ChildLayer => m_ChildLayers;
        public string Name { get; set; }
        public byte Opacity { get; set; }
        public bool Visible { get; set; }
        public LayerBlendMode BlendMode { get; set; }
        public Surface Surface { get; set; }
        public Rectangle rect { get; set; }
    }
}
