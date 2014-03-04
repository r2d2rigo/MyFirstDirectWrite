///////////////////////////////////////////////////////////////////////////////
//
// Copyright (c) 2012-2014 Rodrigo 'r2d2rigo' Díaz
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
// OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//
///////////////////////////////////////////////////////////////////////////////

using SharpDX;
using System;
using SharpDX.Direct2D1;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Windows.ApplicationModel.Core;
using Windows.Graphics.Display;
using Windows.System;
using Windows.UI.Core;
using SharpDX.DirectWrite;

namespace MyFirstDirectWrite
{
    /// <summary>
    /// The view provider class that will handle all the view operations (update/draw).
    /// </summary>
    internal class MyViewProvider : IFrameworkView
    {
        private CoreWindow window;
        private SharpDX.Direct3D11.Device1 device;
        private SharpDX.Direct3D11.DeviceContext1 d3dContext;
        private SharpDX.Direct2D1.DeviceContext d2dContext;
        private SwapChain1 swapChain;
        private SharpDX.Direct2D1.Bitmap1 d2dTarget;
        private TextFormat textFormat;
        private TextLayout textLayout1;
        private TextLayout textLayout2;
        private float layoutY;

        private SolidColorBrush backgroundBrush;
        private SolidColorBrush textBrush;

        /// <summary>
        /// This function is called before SetWindow, so we can't do much yet.
        /// </summary>
        /// <param name="applicationView"></param>
        public void Initialize(CoreApplicationView applicationView)
        {
        }

        /// <summary>
        /// Now that we have a CoreWindow object, the DirectX device/context can be created.
        /// </summary>
        /// <param name="entryPoint"></param>
        public async void Load(string entryPoint)
        {
            // Get the default hardware device and enable debugging. Don't care about the available feature level.
            // DeviceCreationFlags.BgraSupport must be enabled to allow Direct2D interop.
            SharpDX.Direct3D11.Device defaultDevice = new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.Debug | DeviceCreationFlags.BgraSupport);

            // Query the default device for the supported device and context interfaces.
            device = defaultDevice.QueryInterface<SharpDX.Direct3D11.Device1>();
            d3dContext = device.ImmediateContext.QueryInterface<SharpDX.Direct3D11.DeviceContext1>();

            // Query for the adapter and more advanced DXGI objects.
            SharpDX.DXGI.Device2 dxgiDevice2 = device.QueryInterface<SharpDX.DXGI.Device2>();
            SharpDX.DXGI.Adapter dxgiAdapter = dxgiDevice2.Adapter;
            SharpDX.DXGI.Factory2 dxgiFactory2 = dxgiAdapter.GetParent<SharpDX.DXGI.Factory2>();

            // Description for our swap chain settings.
            SwapChainDescription1 description = new SwapChainDescription1()
            {
                // 0 means to use automatic buffer sizing.
                Width = 0,
                Height = 0,
                // 32 bit RGBA color.
                Format = Format.B8G8R8A8_UNorm,
                // No stereo (3D) display.
                Stereo = false,
                // No multisampling.
                SampleDescription = new SampleDescription(1, 0),
                // Use the swap chain as a render target.
                Usage = Usage.RenderTargetOutput,
                // Enable double buffering to prevent flickering.
                BufferCount = 2,
                // No scaling.
                Scaling = Scaling.None,
                // Flip between both buffers.
                SwapEffect = SwapEffect.FlipSequential,
            };

            // Generate a swap chain for our window based on the specified description.
            swapChain = dxgiFactory2.CreateSwapChainForCoreWindow(device, new ComObject(window), ref description, null);

            // Get the default Direct2D device and create a context.
            SharpDX.Direct2D1.Device d2dDevice = new SharpDX.Direct2D1.Device(dxgiDevice2);
            d2dContext = new SharpDX.Direct2D1.DeviceContext(d2dDevice, SharpDX.Direct2D1.DeviceContextOptions.None);

            // Specify the properties for the bitmap that we will use as the target of our Direct2D operations.
            // We want a 32-bit BGRA surface with premultiplied alpha.
            BitmapProperties1 properties = new BitmapProperties1(new PixelFormat(SharpDX.DXGI.Format.B8G8R8A8_UNorm, SharpDX.Direct2D1.AlphaMode.Premultiplied),
                DisplayProperties.LogicalDpi, DisplayProperties.LogicalDpi, BitmapOptions.Target | BitmapOptions.CannotDraw);

            // Get the default surface as a backbuffer and create the Bitmap1 that will hold the Direct2D drawing target.
            Surface backBuffer = swapChain.GetBackBuffer<Surface>(0);
            d2dTarget = new Bitmap1(d2dContext, backBuffer, properties);

            // Create the DirectWrite factory objet.
            SharpDX.DirectWrite.Factory fontFactory = new SharpDX.DirectWrite.Factory();

            // Create a TextFormat object that will use the Segoe UI font with a size of 24 DIPs.
            textFormat = new TextFormat(fontFactory, "Segoe UI", 24.0f);

            // Create two TextLayout objects for rendering the moving text.
            textLayout1 = new TextLayout(fontFactory, "This is an example of a moving TextLayout object with snapped pixel boundaries.", textFormat, 400.0f, 200.0f);
            textLayout2 = new TextLayout(fontFactory, "This is an example of a moving TextLayout object with no snapped pixel boundaries.", textFormat, 400.0f, 200.0f);

            // Vertical offset for the moving text.
            layoutY = 0.0f;

            // Create the brushes for the text background and text color.
            backgroundBrush = new SolidColorBrush(d2dContext, Color.White);
            textBrush = new SolidColorBrush(d2dContext, Color.Black);
        }

        /// <summary>
        /// Run our application until the user quits.
        /// </summary>
        public void Run()
        {
            // Make window active and hide mouse cursor.
            window.PointerCursor = null;
            window.Activate();

            // Infinite loop to prevent the application from exiting.
            while (true)
            {
                // Dispatch all pending events in the queue.
                window.Dispatcher.ProcessEvents(CoreProcessEventsOption.ProcessAllIfPresent);

                // Quit if the users presses Escape key.
                if (window.GetAsyncKeyState(VirtualKey.Escape) == CoreVirtualKeyStates.Down)
                {
                    return;
                }

                // Set the Direct2D drawing target.
                d2dContext.Target = d2dTarget;

                // Clear the target. 
                d2dContext.BeginDraw();
                d2dContext.Clear(Color.CornflowerBlue);

                // Draw a block of text that will be clipped against the specified layout rectangle.
                d2dContext.FillRectangle(new RectangleF(50, 50, 200, 200), backgroundBrush);
                d2dContext.DrawText("This text is long enough to overflow the designed region but will be clipped to the containing rectangle. Lorem ipsum dolor sit amet, consectetur adipiscing elit. ", textFormat, new RectangleF(50, 50, 200, 200), textBrush, DrawTextOptions.Clip);

                // Draw a block of text that will overflow the specified layout rectangle.
                d2dContext.FillRectangle(new RectangleF(50, 300, 200, 200), backgroundBrush);
                d2dContext.DrawText("However, this other text isn't going to be clipped: Lorem ipsum dolor sit amet, consectetur adipiscing elit. Aenean gravida dui id accumsan dictum.", textFormat, new RectangleF(50, 300, 200, 200), textBrush, DrawTextOptions.None);

                // Draw three lines of text with different measuring modes.
                d2dContext.FillRectangle(new RectangleF(300, 50, 400, 200), backgroundBrush);
                d2dContext.DrawText("MeasuringMode: Natural", textFormat, new RectangleF(300, 50, 400, 200), textBrush, DrawTextOptions.None, MeasuringMode.Natural);
                d2dContext.DrawText("MeasuringMode: GDI classic", textFormat, new RectangleF(300, 80, 400, 200), textBrush, DrawTextOptions.None, MeasuringMode.GdiClassic);
                d2dContext.DrawText("MeasuringMode: GDI natural", textFormat, new RectangleF(300, 110, 400, 200), textBrush, DrawTextOptions.None, MeasuringMode.GdiNatural);

                float layoutYOffset = (float)Math.Cos(layoutY) * 50.0f;

                // Draw moving text.
                d2dContext.FillRectangle(new RectangleF(300, 300, 400, 200), backgroundBrush);
                d2dContext.DrawTextLayout(new Vector2(300, 350 + layoutYOffset), textLayout1, textBrush);

                // Draw moving text without pixel snapping, thus giving a smoother movement.
                d2dContext.FillRectangle(new RectangleF(750, 300, 400, 200), backgroundBrush);
                d2dContext.DrawTextLayout(new Vector2(750, 350 + layoutYOffset), textLayout2, textBrush, DrawTextOptions.NoSnap);

                d2dContext.EndDraw();
                layoutY += 1.0f / 60.0f;

                // Present the current buffer to the screen.
                swapChain.Present(1, PresentFlags.None);
            }
        }

        /// <summary>
        /// Sets the window where the app will be rendered.
        /// </summary>
        /// <param name="window">Our main window</param>
        public void SetWindow(CoreWindow window)
        {
            this.window = window;
        }

        /// <summary>
        /// Dispose all the created objects.
        /// </summary>
        public void Uninitialize()
        {
            textLayout2.Dispose();
            textLayout1.Dispose();
            textFormat.Dispose();
            backgroundBrush.Dispose();
            textBrush.Dispose();
            swapChain.Dispose();
            d2dTarget.Dispose();
            d3dContext.Dispose();
            d2dContext.Dispose();
            device.Dispose();
        }
    }
}
