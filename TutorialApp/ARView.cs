using System;
using Android.Content;
using Android.Content.Res;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Util;
using Javax.Microedition.Khronos.Egl;

namespace TutorialApp
{
    public class ARView : GLTextureView
    {
        private Context _context;
        //our renderer implementation
        private ARRenderer _renderer;

        /* Called when device configuration has changed */
        protected override void OnConfigurationChanged(Configuration newConfig)
        {
            //here we force our layout to fill the parent
            if (Parent is FrameLayout)
            {
                LayoutParameters = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.MatchParent,
                        FrameLayout.LayoutParams.MatchParent, GravityFlags.Center);
            }
            else if (Parent is RelativeLayout)
            {
                LayoutParameters = new RelativeLayout.LayoutParams(RelativeLayout.LayoutParams.MatchParent,
                        RelativeLayout.LayoutParams.MatchParent);
            }
        }

        /* Called when layout is created or modified (i.e. because of device rotation changes etc.) */
        protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
        {
            if (!changed) return;
            int angle = 0;
            int geo_angle = 0;
            //here we compute a normalized orientation independent of the device class (tablet or phone)
            //so that an angle of 0 is always landscape, 90 always portrait etc.
            var windowmanager = _context.GetSystemService(Context.WindowService).JavaCast<IWindowManager>();
            Display display = windowmanager.DefaultDisplay;
            int rotation = (int)display.Rotation;
            if (Resources.Configuration.Orientation == Android.Content.Res.Orientation.Landscape)
            {
                switch (rotation)
                {
                    case 0: angle = 0; geo_angle = 0; break;
                    case 1: angle = 0; geo_angle = 270; break;
                    case 2: angle = 180; geo_angle = 180; break;

                    case 3: angle = 180; geo_angle = 90; break;
                    default:
                        break;
                }
            }
            else
            {
                switch (rotation)
                {
                    case 0: angle = 90; geo_angle = 0; break;

                    case 1: angle = 270; geo_angle = 270; break;

                    case 2: angle = 270; geo_angle = 180; break;

                    case 3: angle = 90; geo_angle = 90; break;
                    default:
                        break;
                }
            }

            int realWidth;
            int realHeight;
            if ((int)Build.VERSION.SdkInt >= 17)
            {
                //new pleasant way to get real metrics
                DisplayMetrics realMetrics = new DisplayMetrics();
                display.GetRealMetrics(realMetrics);
                realWidth = realMetrics.WidthPixels;
                realHeight = realMetrics.HeightPixels;

            }
            else if ((int)Build.VERSION.SdkInt >= 14)
            {
                //reflection for this weird in-between time
                try
                {
                    Java.Lang.Reflect.Method mGetRawH = Display.Class.GetMethod("getRawHeight");
                    var mGetRawW = Display.Class.GetMethod("getRawWidth");
                    realWidth = (int)mGetRawW.Invoke(display);
                    realHeight = (int)mGetRawH.Invoke(display);
                }
                catch (Exception e)
                {
                    //this may not be 100% accurate, but it's all we've got
                    realWidth = display.Width;
                    realHeight = display.Height;
                }
            }
            else
            {
                //This should be close, as lower API devices should not have window navigation bars
                realWidth = display.Width;
                realHeight = display.Height;
            }
            _renderer.UpdateViewport(right - left, bottom - top, angle, geo_angle);
        }

        /* Constructor. */
        public ARView(Context context) : base(context)
        {
            _context = context;
            init();
            _renderer = new ARRenderer(this._context);
            setRenderer(_renderer);
            ((ARRenderer)_renderer).IsActive = true;
            SetOpaque(true);
        }

        /* Initialization. */
        public void init()
        {
            setEGLContextFactory(new ContextFactory());
            setEGLConfigChooser(new ConfigChooser(8, 8, 8, 0, 16, 0));
        }

        /* Checks the OpenGL error.*/
        private static void checkEglError(String prompt, IEGL10 egl)
        {
            int error;
            while ((error = egl.EglGetError()) != EGL10.EglSuccess)
            {
                Log.Error("PikkartCore3", String.Format("%s: EGL error: 0x%x", prompt, error));
            }
        }

        /* A private class that manages the creation of OpenGL contexts. Pretty standard stuff*/
        private class ContextFactory : EGLContextFactory
        {
            private static int EGL_CONTEXT_CLIENT_VERSION = 0x3098;

            public EGLContext createContext(IEGL10 egl, EGLDisplay display, EGLConfig eglConfig)
            {
                EGLContext context;
                //Log.i("PikkartCore3","Creating OpenGL ES 2.0 context");
                checkEglError("Before eglCreateContext", egl);
                int[] attrib_list_gl20 = { EGL_CONTEXT_CLIENT_VERSION, 2, EGL10.EglNone };
                context = egl.EglCreateContext(display, eglConfig, EGL10.EglNoContext, attrib_list_gl20);
                checkEglError("After eglCreateContext", egl);
                return context;
            }

            public void destroyContext(IEGL10 egl, EGLDisplay display, EGLContext context)
            {
                egl.EglDestroyContext(display, context);
            }
        }


        /* A private class that manages the the config chooser. Pretty standard stuff */
        private class ConfigChooser : EGLConfigChooser
        {
            public ConfigChooser(int r, int g, int b, int a, int depth, int stencil)
            {
                mRedSize = r;
                mGreenSize = g;
                mBlueSize = b;
                mAlphaSize = a;
                mDepthSize = depth;
                mStencilSize = stencil;
            }

            private EGLConfig getMatchingConfig(IEGL10 egl, EGLDisplay display, int[] configAttribs)
            {
                // Get the number of minimally matching EGL configurations
                int[] num_config = new int[1];
                egl.EglChooseConfig(display, configAttribs, null, 0, num_config);
                int numConfigs = num_config[0];
                if (numConfigs <= 0)
                    throw new Exception("No matching EGL configs");
                // Allocate then read the array of minimally matching EGL configs
                EGLConfig[] configs = new EGLConfig[numConfigs];
                egl.EglChooseConfig(display, configAttribs, configs, numConfigs, num_config);
                // Now return the "best" one
                return chooseConfig(egl, display, configs);
            }

            public EGLConfig chooseConfig(IEGL10 egl, EGLDisplay display)
            {
                // This EGL config specification is used to specify 2.0 com.pikkart.ar.rendering. We use a minimum size of 4 bits for
                // red/green/blue, but will perform actual matching in chooseConfig() below.
                int EGL_OPENGL_ES2_BIT = 0x0004;
                int[] s_configAttribs_gl20 = {EGL10.EglRedSize, 4, EGL10.EglGreenSize, 4, EGL10.EglBlueSize, 4,
                    EGL10.EglRenderableType, EGL_OPENGL_ES2_BIT, EGL10.EglNone};
                return getMatchingConfig(egl, display, s_configAttribs_gl20);
            }

            public EGLConfig chooseConfig(IEGL10 egl, EGLDisplay display, EGLConfig[] configs)
            {
                bool bFoundDepth = false;
                foreach (EGLConfig config in configs)
                {
                    int d = findConfigAttrib(egl, display, config, EGL10.EglDepthSize, 0);
                    if (d == mDepthSize) bFoundDepth = true;
                }
                if (bFoundDepth == false) mDepthSize = 16; //min value
                foreach (EGLConfig config in configs)
                {
                    int d = findConfigAttrib(egl, display, config, EGL10.EglDepthSize, 0);
                    int s = findConfigAttrib(egl, display, config, EGL10.EglStencilSize, 0);
                    // We need at least mDepthSize and mStencilSize bits
                    if (d < mDepthSize || s < mStencilSize)
                        continue;
                    // We want an *exact* match for red/green/blue/alpha
                    int r = findConfigAttrib(egl, display, config, EGL10.EglRedSize, 0);
                    int g = findConfigAttrib(egl, display, config, EGL10.EglGreenSize, 0);
                    int b = findConfigAttrib(egl, display, config, EGL10.EglBlueSize, 0);
                    int a = findConfigAttrib(egl, display, config, EGL10.EglAlphaSize, 0);

                    if (r == mRedSize && g == mGreenSize && b == mBlueSize && a == mAlphaSize)
                        return config;
                }

                return null;
            }

            private int findConfigAttrib(IEGL10 egl, EGLDisplay display, EGLConfig config, int attribute, int defaultValue)
            {
                if (egl.EglGetConfigAttrib(display, config, attribute, mValue))
                    return mValue[0];
                return defaultValue;
            }

            // Subclasses can adjust these values:
            protected int mRedSize;
            protected int mGreenSize;
            protected int mBlueSize;
            protected int mAlphaSize;
            protected int mDepthSize;
            protected int mStencilSize;
            private int[] mValue = new int[1];
        }
    }
}