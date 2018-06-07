using Android.Content;
using Android.Graphics;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Javax.Microedition.Khronos.Egl;
using Javax.Microedition.Khronos.Opengles;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Runnable = Java.Lang.Runnable;
using StringBuilder = Java.Lang.StringBuilder;

namespace TutorialApp
{

    public class GLTextureView : TextureView, TextureView.ISurfaceTextureListener, View.IOnLayoutChangeListener
    {
        private static string TAG = "GLSurfaceView";
        private static bool LOG_ATTACH_DETACH = false;
        private static bool LOG_THREADS = false;
        private static bool LOG_PAUSE_RESUME = false;
        private static bool LOG_SURFACE = false;
        private static bool LOG_RENDERER = false;
        private static bool LOG_RENDERER_DRAW_FRAME = false;
        private static bool LOG_EGL = false;

        public static int RENDERMODE_WHEN_DIRTY = 0;
        public static int RENDERMODE_CONTINUOUSLY = 1;
        public static int DEBUG_CHECK_GL_ERROR = 1;
        public static int DEBUG_LOG_GL_CALLS = 2;

        private static GLThreadManager sGLThreadManager;

        private WeakReference<GLTextureView> mThisWeakRef;
        private GLThread mGLThread;
        private Renderer mRenderer;
        private bool mDetached;
        private EGLConfigChooser mEGLConfigChooser;
        private EGLContextFactory mEGLContextFactory;
        private EGLWindowSurfaceFactory mEGLWindowSurfaceFactory;
        private GLWrapper mGLWrapper;
        private int mDebugFlags;
        private int mEGLContextClientVersion;
        private bool mPreserveEGLContextOnPause;        

        public GLTextureView(Context context) : base(context)
        {
            mThisWeakRef = new WeakReference<GLTextureView>(this);
            sGLThreadManager = new GLThreadManager(this);
            init();
        }

        public GLTextureView(Context context, IAttributeSet attrs) : base(context, attrs)
        {
            init();
        }

        ~GLTextureView()
        {
            try
            {
                if (mGLThread != null)
                {
                    // GLThread may still be running if this view was never attached to a window.
                    mGLThread.requestExitAndWait();
                }
            }
            finally
            {
                //base.Finalize();
            }
        }

        private void init()
        {
            SurfaceTextureListener = this;
            AddOnLayoutChangeListener(this);
        }

        public void setGLWrapper(GLWrapper glWrapper)
        {
            mGLWrapper = glWrapper;
        }

        public void setDebugFlags(int debugFlags)
        {
            mDebugFlags = debugFlags;
        }

        public int getDebugFlags()
        {
            return mDebugFlags;
        }

        public void setPreserveEGLContextOnPause(bool preserveOnPause)
        {
            mPreserveEGLContextOnPause = preserveOnPause;
        }

        public bool getPreserveEGLContextOnPause()
        {
            return mPreserveEGLContextOnPause;
        }

        public void setRenderer(Renderer renderer)
        {
            checkRenderThreadState();
            if (mEGLConfigChooser == null)
            {
                mEGLConfigChooser = new SimpleEGLConfigChooser(this, true);
            }
            if (mEGLContextFactory == null)
            {
                mEGLContextFactory = new DefaultContextFactory(this);
            }
            if (mEGLWindowSurfaceFactory == null)
            {
                mEGLWindowSurfaceFactory = new DefaultWindowSurfaceFactory();
            }
            mRenderer = renderer;
            mGLThread = new GLThread(mThisWeakRef);
            mGLThread.Start();
        }

        public void setEGLContextFactory(EGLContextFactory factory)
        {
            checkRenderThreadState();
            mEGLContextFactory = factory;
        }

        public void setEGLWindowSurfaceFactory(EGLWindowSurfaceFactory factory)
        {
            checkRenderThreadState();
            mEGLWindowSurfaceFactory = factory;
        }

        public void setEGLConfigChooser(EGLConfigChooser configChooser)
        {
            checkRenderThreadState();
            mEGLConfigChooser = configChooser;
        }

        public void setEGLConfigChooser(bool needDepth)
        {
            setEGLConfigChooser(new SimpleEGLConfigChooser(this, needDepth));
        }

        public void setEGLConfigChooser(int redSize, int greenSize, int blueSize, int alphaSize, int depthSize, int stencilSize)
        {
            setEGLConfigChooser(new ComponentSizeChooser(this, redSize, greenSize, blueSize, alphaSize, depthSize, stencilSize));
        }

        public void setEGLContextClientVersion(int version)
        {
            checkRenderThreadState();
            mEGLContextClientVersion = version;
        }

        public void setRenderMode(int renderMode)
        {
            mGLThread.setRenderMode(renderMode);
        }

        public int getRenderMode()
        {
            return mGLThread.getRenderMode();
        }

        public void OnSurfaceTextureUpdated(SurfaceTexture surface)
        {
            mGLThread.requestRender();
        }

        public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
        {
            mGLThread.surfaceCreated();
        }

        public bool OnSurfaceTextureDestroyed(SurfaceTexture surface)
        {
            // Surface will be destroyed when we return
            mGLThread.surfaceDestroyed();
            return true;
        }

        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int w, int h)
        {
            mGLThread.onWindowResize(w, h);
        }

        public void onPause()
        {
            mGLThread.onPause();
        }

        public void onResume()
        {
            mGLThread.onResume();
        }

        public void queueEvent(Runnable r)
        {
            mGLThread.queueEvent(r);
        }

        protected override void OnAttachedToWindow()
        {
            base.OnAttachedToWindow();
            if (LOG_ATTACH_DETACH)
            {
                Log.Debug(TAG, "onAttachedToWindow reattach =" + mDetached);
            }
            if (mDetached && (mRenderer != null))
            {
                int renderMode = RENDERMODE_CONTINUOUSLY;
                if (mGLThread != null)
                {
                    renderMode = mGLThread.getRenderMode();
                }
                mGLThread = new GLThread(mThisWeakRef);
                if (renderMode != RENDERMODE_CONTINUOUSLY)
                {
                    mGLThread.setRenderMode(renderMode);
                }
                mGLThread.Start();
            }
            mDetached = false;
        }

        protected override void OnDetachedFromWindow()
        {
            if (LOG_ATTACH_DETACH)
            {
                Log.Debug(TAG, "onDetachedFromWindow");
            }
            if (mGLThread != null)
            {
                mGLThread.requestExitAndWait();
            }
            mDetached = true;
            base.OnDetachedFromWindow();
        }

        /*public void OnLayoutChangeListener()
        {
            surfaceChanged(getSurfaceTexture(), 0, right - left, bottom - top);
        }*/
        // ----------------------------------------------------------------------

        public interface GLWrapper
        {
            /**
             * Wraps a gl interface in another gl interface.
             * @param gl a IGL interface that is to be wrapped.
             * @return either the input argument or another IGL object that wraps the input argument.
             */
            IGL wrap(IGL gl);
        }

        public interface Renderer
        {
            /**
             * Called when the surface is created or recreated.
             * <p>
             * Called when the com.pikkart.ar.rendering thread
             * starts and whenever the EGL context is lost. The EGL context will typically
             * be lost when the Android device awakes after going to sleep.
             * <p>
             * Since this method is called at the beginning of com.pikkart.ar.rendering, as well as
             * every time the EGL context is lost, this method is a convenient place to put
             * code to create resources that need to be created when the com.pikkart.ar.rendering
             * starts, and that need to be recreated when the EGL context is lost.
             * Textures are an example of a resource that you might want to create
             * here.
             * <p>
             * Note that when the EGL context is lost, all OpenGL resources associated
             * with that context will be automatically deleted. You do not need to call
             * the corresponding "glDelete" methods such as glDeleteTextures to
             * manually delete these lost resources.
             * <p>
             * @param gl the IGL interface. Use <code>instanceof</code> to
             * test if the interface supports GL11 or higher interfaces.
             * @param config the EGLConfig of the created surface. Can be used
             * to create matching pbuffers.
             */
            void OnSurfaceCreated(IGL10 gl, EGLConfig config);

            /**
             * Called when the surface changed size.
             * <p>
             * Called after the surface is created and whenever
             * the OpenGL ES surface size changes.
             * <p>
             * Typically you will set your viewport here. If your camera
             * is fixed then you could also set your projection matrix here:
             * <pre class="prettyprint">
             * void onSurfaceChanged(IGL10 gl, int width, int height) {
             *     gl.glViewport(0, 0, width, height);
             *     // for a fixed camera, set the projection too
             *     float ratio = (float) width / height;
             *     gl.glMatrixMode(GL10.GL_PROJECTION);
             *     gl.glLoadIdentity();
             *     gl.glFrustumf(-ratio, ratio, -1, 1, 1, 10);
             * }
             * </pre>
             * @param gl the IGL interface. Use <code>instanceof</code> to
             * test if the interface supports GL11 or higher interfaces.
             * @param width
             * @param height
             */
            void onSurfaceChanged(IGL10 gl, int width, int height);

            void onSurfaceDestroyed();

            /**
             * Called to draw the current frame.
             * <p>
             * This method is responsible for drawing the current frame.
             * <p>
             * The implementation of this method typically looks like this:
             * <pre class="prettyprint">
             * void onDrawFrame(IGL10 gl) {
             *     gl.glClear(GL10.GL_COLOR_BUFFER_BIT | GL10.GL_DEPTH_BUFFER_BIT);
             *     //... other gl calls to render the scene ...
             * }
             * </pre>
             * @param gl the IGL interface. Use <code>instanceof</code> to
             * test if the interface supports GL11 or higher interfaces.
             */
            void onDrawFrame(IGL10 gl);
        }

        public interface EGLContextFactory
        {
            EGLContext createContext(IEGL10 egl, EGLDisplay display, EGLConfig eglConfig);
            void destroyContext(IEGL10 egl, EGLDisplay display, EGLContext context);
        }

        private class DefaultContextFactory : EGLContextFactory
        {
            private int EGL_CONTEXT_CLIENT_VERSION = 0x3098;
            GLTextureView Parent;

            public DefaultContextFactory(GLTextureView parent)
            {
                Parent = parent;
            }

            public EGLContext createContext(IEGL10 egl, EGLDisplay display, EGLConfig config)
            {
                int[] attrib_list = { EGL_CONTEXT_CLIENT_VERSION, Parent.mEGLContextClientVersion, EGL10.EglNone };

                return egl.EglCreateContext(display, config, EGL10.EglNoContext, Parent.mEGLContextClientVersion != 0 ? attrib_list : null);
            }

            public void destroyContext(IEGL10 egl, EGLDisplay display, EGLContext context)
            {
                if (!egl.EglDestroyContext(display, context))
                {
                    Log.Error("DefaultContextFactory", "display:" + display + " context: " + context);
                    if (LOG_THREADS)
                    {
                        Log.Info("DefaultContextFactory", "tid=" + Thread.CurrentThread.ManagedThreadId);
                    }
                    EglHelper.throwEglException("eglDestroyContex", egl.EglGetError());
                }
            }
        }

        public interface EGLWindowSurfaceFactory
        {
            /**
             *  @return null if the surface cannot be constructed.
             */
            EGLSurface createWindowSurface(IEGL10 egl, EGLDisplay display, EGLConfig config, Java.Lang.Object nativeWindow);
            void destroySurface(IEGL10 egl, EGLDisplay display, EGLSurface surface);
        }

        class DefaultWindowSurfaceFactory : EGLWindowSurfaceFactory
        {
            public EGLSurface createWindowSurface(IEGL10 egl, EGLDisplay display, EGLConfig config, Java.Lang.Object nativeWindow)
            {
                EGLSurface result = null;
                try
                {
                    result = egl.EglCreateWindowSurface(display, config, nativeWindow, null);
                }
                catch (System.Exception e)
                {
                    // This exception indicates that the surface flinger surface
                    // is not valid. This can happen if the surface flinger surface has
                    // been torn down, but the application has not yet been
                    // notified via SurfaceHolder.Callback.surfaceDestroyed.
                    // In theory the application should be notified first,
                    // but in practice sometimes it is not. See b/4588890
                    Log.Error(TAG, "eglCreateWindowSurface", e);
                }
                return result;
            }

            public void destroySurface(IEGL10 egl, EGLDisplay display, EGLSurface surface)
            {
                egl.EglDestroySurface(display, surface);
            }
        }

        public interface EGLConfigChooser
        {
            /**
             * Choose a configuration from the list. Implementors typically
             * implement this method by calling
             * {@link EGL10#eglChooseConfig} and iterating through the results. Please consult the
             * EGL specification available from The Khronos Group to learn how to call eglChooseConfig.
             * @param egl the IEGL10 for the current display.
             * @param display the current display.
             * @return the chosen configuration.
             */
            EGLConfig chooseConfig(IEGL10 egl, EGLDisplay display);
        }

        private abstract class BaseConfigChooser : EGLConfigChooser
        {
            GLTextureView Parent;
            public BaseConfigChooser(GLTextureView parent, int[] configSpec)
            {
                Parent = parent;
                mConfigSpec = filterConfigSpec(configSpec);
            }

            public EGLConfig chooseConfig(IEGL10 egl, EGLDisplay display)
            {
                int[] num_config = new int[1];
                if (!egl.EglChooseConfig(display, mConfigSpec, null, 0, num_config))
                {
                    throw new Exception("eglChooseConfig failed");
                }

                int numConfigs = num_config[0];

                if (numConfigs <= 0)
                {
                    throw new Exception("No configs match configSpec");
                }

                EGLConfig[] configs = new EGLConfig[numConfigs];
                if (!egl.EglChooseConfig(display, mConfigSpec, configs, numConfigs, num_config))
                {
                    throw new Exception("eglChooseConfig#2 failed");
                }
                EGLConfig config = chooseConfig(egl, display, configs);
                if (config == null)
                {
                    throw new Exception("No config chosen");
                }
                return config;
            }

            public abstract EGLConfig chooseConfig(IEGL10 egl, EGLDisplay display, EGLConfig[] configs);

            protected int[] mConfigSpec;

            private int[] filterConfigSpec(int[] configSpec)
            {
                if (Parent.mEGLContextClientVersion != 2)
                {
                    return configSpec;
                }
                /* We know none of the subclasses define EGL_RENDERABLE_TYPE.
                 * And we know the configSpec is well formed.
                 */
                int len = configSpec.Length;
                int[] newConfigSpec = new int[len + 2];
                System.Array.Copy(configSpec, 0, newConfigSpec, 0, len - 1);
                newConfigSpec[len - 1] = EGL10.EglRenderableType;
                newConfigSpec[len] = 4; /* EGL_OPENGL_ES2_BIT */
                newConfigSpec[len + 1] = EGL10.EglNone;
                return newConfigSpec;
            }
        }

        private class ComponentSizeChooser : BaseConfigChooser
        {
            public ComponentSizeChooser(GLTextureView parent, int redSize, int greenSize, int blueSize, int alphaSize, int depthSize, int stencilSize)
                    : base(parent, new int[] {
                    EGL10.EglRedSize, redSize,
                    EGL10.EglGreenSize, greenSize,
                    EGL10.EglBlueSize, blueSize,
                    EGL10.EglAlphaSize, alphaSize,
                    EGL10.EglDepthSize, depthSize,
                    EGL10.EglStencilSize, stencilSize,
                    EGL10.EglNone})
            {
                mValue = new int[1];
                mRedSize = redSize;
                mGreenSize = greenSize;
                mBlueSize = blueSize;
                mAlphaSize = alphaSize;
                mDepthSize = depthSize;
                mStencilSize = stencilSize;
            }

            public override EGLConfig chooseConfig(IEGL10 egl, EGLDisplay display, EGLConfig[] configs)
            {
                foreach (EGLConfig config in configs)
                {
                    int d = findConfigAttrib(egl, display, config, EGL10.EglDepthSize, 0);
                    int s = findConfigAttrib(egl, display, config, EGL10.EglStencilSize, 0);
                    if ((d >= mDepthSize) && (s >= mStencilSize))
                    {
                        int r = findConfigAttrib(egl, display, config, EGL10.EglRedSize, 0);
                        int g = findConfigAttrib(egl, display, config, EGL10.EglGreenSize, 0);
                        int b = findConfigAttrib(egl, display, config, EGL10.EglBlueSize, 0);
                        int a = findConfigAttrib(egl, display, config, EGL10.EglAlphaSize, 0);
                        if ((r == mRedSize) && (g == mGreenSize) && (b == mBlueSize) && (a == mAlphaSize))
                        {
                            return config;
                        }
                    }
                }
                return null;
            }

            private int findConfigAttrib(IEGL10 egl, EGLDisplay display, EGLConfig config, int attribute, int defaultValue)
            {
                if (egl.EglGetConfigAttrib(display, config, attribute, mValue))
                {
                    return mValue[0];
                }
                return defaultValue;
            }

            private int[] mValue;
            // Subclasses can adjust these values:
            protected int mRedSize;
            protected int mGreenSize;
            protected int mBlueSize;
            protected int mAlphaSize;
            protected int mDepthSize;
            protected int mStencilSize;
        }

        private class SimpleEGLConfigChooser : ComponentSizeChooser
        {
            public SimpleEGLConfigChooser(GLTextureView Parent, bool withDepthBuffer) : base(Parent, 8, 8, 8, 0, withDepthBuffer ? 16 : 0, 0)
            {
            }
        }

        private class EglHelper
        {
            public EglHelper(WeakReference<GLTextureView> glSurfaceViewWeakRef)
            {
                mGLSurfaceViewWeakRef = glSurfaceViewWeakRef;
            }

            /**
             * Initialize EGL for a given configuration spec.
             * @param configSpec
             */
            public void start()
            {
                if (LOG_EGL)
                {
                    Log.Debug("EglHelper", "start() tid=" + Thread.CurrentThread.ManagedThreadId);
                }
                /*
                 * Get an EGL instance
                 */
                mEgl = EGLContext.EGL.JavaCast<IEGL10>();

                /*
                 * Get to the default display.
                 */
                mEglDisplay = mEgl.EglGetDisplay(EGL10.EglDefaultDisplay);

                if (mEglDisplay == EGL10.EglNoDisplay)
                {
                    throw new Exception("eglGetDisplay failed");
                }

                /*
                 * We can now initialize EGL for that display
                 */
                int[] version = new int[2];
                if (!mEgl.EglInitialize(mEglDisplay, version))
                {
                    throw new Exception("eglInitialize failed");
                }
                GLTextureView view;
                if(!mGLSurfaceViewWeakRef.TryGetTarget(out view))
                {
                    mEglConfig = null;
                    mEglContext = null;
                }
                else
                {
                    mEglConfig = view.mEGLConfigChooser.chooseConfig(mEgl, mEglDisplay);
                    /*
                    * Create an EGL context. We want to do this as rarely as we can, because an
                    * EGL context is a somewhat heavy object.
                    */
                    mEglContext = view.mEGLContextFactory.createContext(mEgl, mEglDisplay, mEglConfig);
                }
                if (mEglContext == null || mEglContext == EGL10.EglNoContext)
                {
                    mEglContext = null;
                    throwEglException("createContext");
                }
                if (LOG_EGL)
                {
                    Log.Debug("EglHelper", "createContext " + mEglContext + " tid=" + Thread.CurrentThread.ManagedThreadId);
                }
                mEglSurface = null;
            }

            /**
             * Create an egl surface for the current SurfaceHolder surface. If a surface
             * already exists, destroy it before creating the new surface.
             *
             * @return true if the surface was created successfully.
             */
            public bool createSurface()
            {
                if (LOG_EGL)
                {
                    Log.Debug("EglHelper", "createSurface()  tid=" + Thread.CurrentThread.ManagedThreadId);
                }
                /*
                 * Check preconditions.
                 */
                if (mEgl == null)
                {
                    throw new Exception("egl not initialized");
                }
                if (mEglDisplay == null)
                {
                    throw new Exception("eglDisplay not initialized");
                }
                if (mEglConfig == null)
                {
                    throw new Exception("mEglConfig not initialized");
                }
                /*
                 *  The window size has changed, so we need to create a new
                 *  surface.
                 */
                destroySurfaceImp();
                /*
                 * Create an EGL surface we can render into.
                 */
                GLTextureView view;
                if (mGLSurfaceViewWeakRef.TryGetTarget(out view))
                {
                    mEglSurface = view.mEGLWindowSurfaceFactory.createWindowSurface(mEgl, mEglDisplay, mEglConfig, view.SurfaceTexture);
                }
                else
                {
                    mEglSurface = null;
                }
                if (mEglSurface == null || mEglSurface == EGL10.EglNoSurface)
                {
                    int error = mEgl.EglGetError();
                    if (error == EGL10.EglBadNativeWindow)
                    {
                        Log.Error("EglHelper", "createWindowSurface returned EGL_BAD_NATIVE_WINDOW.");
                    }
                    return false;
                }
                /*
                 * Before we can issue IGL commands, we need to make sure
                 * the context is current and bound to a surface.
                 */
                if (!mEgl.EglMakeCurrent(mEglDisplay, mEglSurface, mEglSurface, mEglContext))
                {
                    /*
                     * Could not make the context current, probably because the underlying
                     * SurfaceView surface has been destroyed.
                     */
                    logEglErrorAsWarning("EGLHelper", "eglMakeCurrent", mEgl.EglGetError());
                    return false;
                }
                return true;
            }

            /**
             * Create a IGL object for the current EGL context.
             * @return
             */
            public IGL createGL()
            {
                IGL gl = mEglContext.GL;
                GLTextureView view;
                if (mGLSurfaceViewWeakRef.TryGetTarget(out view))
                {
                    if (view.mGLWrapper != null)
                    {
                        gl = view.mGLWrapper.wrap(gl);
                    }

                    if ((view.mDebugFlags & (DEBUG_CHECK_GL_ERROR | DEBUG_LOG_GL_CALLS)) != 0)
                    {
                        int configFlags = 0;
                        Java.IO.Writer log = null;
                        if ((view.mDebugFlags & DEBUG_CHECK_GL_ERROR) != 0)
                        {
                            configFlags |= (int)Android.Opengl.GLDebugHelper.ConfigCheckGlError;
                        }
                        if ((view.mDebugFlags & DEBUG_LOG_GL_CALLS) != 0)
                        {
                            log = new LogWriter();
                        }
                        gl = Android.Opengl.GLDebugHelper.Wrap(gl, configFlags, log);
                    }
                }
                return gl;
            }

            /**
             * Display the current render surface.
             * @return the EGL error code from eglSwapBuffers.
             */
            public int swap()
            {
                if (!mEgl.EglSwapBuffers(mEglDisplay, mEglSurface))
                {
                    return mEgl.EglGetError();
                }
                return EGL10.EglSuccess;
            }

            public void destroySurface()
            {
                if (LOG_EGL)
                {
                    Log.Debug("EglHelper", "destroySurface()  tid=" + Thread.CurrentThread.ManagedThreadId);
                }

                GLTextureView view;
                if (mGLSurfaceViewWeakRef.TryGetTarget(out view))
                {
                    view.mRenderer.onSurfaceDestroyed();                    
                }
                destroySurfaceImp();
            }

            private void destroySurfaceImp()
            {
                if (mEglSurface != null && mEglSurface != EGL10.EglNoSurface)
                {
                    mEgl.EglMakeCurrent(mEglDisplay, EGL10.EglNoSurface, EGL10.EglNoSurface, EGL10.EglNoContext);
                    GLTextureView view;
                    if (mGLSurfaceViewWeakRef.TryGetTarget(out view))
                    {
                        view.mEGLWindowSurfaceFactory.destroySurface(mEgl, mEglDisplay, mEglSurface);
                    }
                    mEglSurface = null;
                }
            }

            public void finish()
            {
                if (LOG_EGL)
                {
                    Log.Debug("EglHelper", "finish() tid=" + Thread.CurrentThread.ManagedThreadId);
                }
                if (mEglContext != null)
                {
                    GLTextureView view;
                    if (mGLSurfaceViewWeakRef.TryGetTarget(out view))
                    {
                        view.mEGLContextFactory.destroyContext(mEgl, mEglDisplay, mEglContext);
                    }
                    mEglContext = null;
                }
                if (mEglDisplay != null)
                {
                    mEgl.EglTerminate(mEglDisplay);
                    mEglDisplay = null;
                }
            }

            private void throwEglException(string function)
            {
                throwEglException(function, mEgl.EglGetError());
            }

            public static void throwEglException(string function, int error)
            {
                string message = formatEglError(function, error);
                if (LOG_THREADS)
                {
                    Log.Error("EglHelper", "throwEglException tid=" + Thread.CurrentThread.ManagedThreadId + " " + message);
                }
                throw new Exception(message);
            }

            public static void logEglErrorAsWarning(string tag, string function, int error)
            {
                Log.Debug(tag, formatEglError(function, error));
            }

            public static string formatEglError(string function, int error)
            {
                return function + " failed: " + error;
            }

            private WeakReference<GLTextureView> mGLSurfaceViewWeakRef;
            IEGL10 mEgl;
            EGLDisplay mEglDisplay;
            EGLSurface mEglSurface;
            public EGLConfig mEglConfig;
            EGLContext mEglContext;

        }

        public class GLThread
        {
            System.Threading.Thread _thread;

            public GLThread(WeakReference<GLTextureView> glSurfaceViewWeakRef) : base()
            {
                mWidth = 0;
                mHeight = 0;
                mRequestRender = true;
                mRenderMode = RENDERMODE_CONTINUOUSLY;
                mGLSurfaceViewWeakRef = glSurfaceViewWeakRef;
                _thread = new Thread(new ThreadStart(this.Run));
            }

            public void Start()
            {
                _thread.Start();
            }

            public void Run()
            {
                _thread.Name = "GLThread " + _thread.ManagedThreadId;
                if (LOG_THREADS)
                {
                    Log.Info("GLThread", "starting tid=" + _thread.ManagedThreadId);
                }
                try
                {
                    guardedRun();
                }
                catch (Exception e)
                {
                    // fall thru and exit normally
                    throw e;
                }
                finally
                {
                    sGLThreadManager.ThreadExiting(this);
                }
            }

            int Id
            {
                get
                {
                    return _thread.ManagedThreadId;
                }
            }

            string Name
            {
                get
                {
                    return _thread.Name;
                }
                set
                {
                    _thread.Name = value;
                }
            }

            /*
             * This private method should only be called inside a
             * lock(sGLThreadManager) block.
             */
            private void stopEglSurfaceLocked()
            {
                if (mHaveEglSurface)
                {
                    mHaveEglSurface = false;
                    mEglHelper.destroySurface();
                }
            }

            /*
             * This private method should only be called inside a
             * lock(sGLThreadManager) block.
             */
            private void stopEglContextLocked()
            {
                if (mHaveEglContext)
                {
                    mEglHelper.finish();
                    mHaveEglContext = false;
                    sGLThreadManager.releaseEglContextLocked(this);
                }
            }
            
            private void guardedRun()
            {
                mEglHelper = new EglHelper(mGLSurfaceViewWeakRef);
                mHaveEglContext = false;
                mHaveEglSurface = false;
                try
                {
                    IGL10 gl = null;
                    bool createEglContext = false;
                    bool createEglSurface = false;
                    bool createGlInterface = false;
                    bool lostEglContext = false;
                    bool sizeChanged = false;
                    bool wantRenderNotification = false;
                    bool doRenderNotification = false;
                    bool askedToReleaseEglContext = false;
                    int w = 0;
                    int h = 0;
                    Java.Lang.Runnable ev = null;

                    while (true)
                    {
                        lock (sGLThreadManager)
                        {
                            while (true)
                            {
                                if (mShouldExit)
                                {
                                    return;
                                }

                                if (mEventQueue.Count > 0)
                                {
                                    ev = mEventQueue.Dequeue();
                                    break;
                                }

                                // Update the pause state.
                                bool pausing = false;
                                if (mPaused != mRequestPaused)
                                {
                                    pausing = mRequestPaused;
                                    mPaused = mRequestPaused;
                                    Monitor.PulseAll(sGLThreadManager);
                                    if (LOG_PAUSE_RESUME)
                                    {
                                        Log.Info("GLThread", "mPaused is now " + mPaused + " tid=" + Id);
                                    }
                                }

                                // Do we need to give up the EGL context?
                                if (mShouldReleaseEglContext)
                                {
                                    if (LOG_SURFACE)
                                    {
                                        Log.Info("GLThread", "releasing EGL context because asked to tid=" + Id);
                                    }

                                    stopEglSurfaceLocked();
                                    stopEglContextLocked();
                                    mShouldReleaseEglContext = false;
                                    askedToReleaseEglContext = true;
                                }

                                // Have we lost the EGL context?
                                if (lostEglContext)
                                {
                                    stopEglSurfaceLocked();
                                    stopEglContextLocked();
                                    lostEglContext = false;
                                }

                                // When pausing, release the EGL surface:
                                if (pausing && mHaveEglSurface)
                                {
                                    if (LOG_SURFACE)
                                    {
                                        Log.Info("GLThread", "releasing EGL surface because paused tid=" + Id);
                                    }
                                    stopEglSurfaceLocked();
                                }

                                // When pausing, optionally release the EGL Context:
                                if (pausing && mHaveEglContext)
                                {
                                    GLTextureView view = null;
                                    mGLSurfaceViewWeakRef.TryGetTarget(out view);                                    


                                    bool preserveEglContextOnPause = view == null ? false : view.mPreserveEGLContextOnPause;
                                    if (!preserveEglContextOnPause || sGLThreadManager.shouldReleaseEGLContextWhenPausing())
                                    {
                                        stopEglContextLocked();
                                        if (LOG_SURFACE)
                                        {
                                            Log.Info("GLThread", "releasing EGL context because paused tid=" + Id);
                                        }
                                    }
                                }

                                // When pausing, optionally terminate EGL:
                                if (pausing)
                                {
                                    if (sGLThreadManager.shouldTerminateEGLWhenPausing())
                                    {
                                        mEglHelper.finish();
                                        if (LOG_SURFACE)
                                        {
                                            Log.Info("GLThread", "terminating EGL because paused tid=" + Id);
                                        }
                                    }
                                }

                                // Have we lost the SurfaceView surface?
                                if ((!mHasSurface) && (!mWaitingForSurface))
                                {
                                    if (LOG_SURFACE)
                                    {
                                        Log.Info("GLThread", "noticed surfaceView surface lost tid=" + Id);
                                    }
                                    if (mHaveEglSurface)
                                    {
                                        stopEglSurfaceLocked();
                                    }
                                    mWaitingForSurface = true;
                                    mSurfaceIsBad = false;
                                    Monitor.PulseAll(sGLThreadManager);
                                }

                                // Have we acquired the surface view surface?
                                if (mHasSurface && mWaitingForSurface)
                                {
                                    if (LOG_SURFACE)
                                    {
                                        Log.Info("GLThread", "noticed surfaceView surface acquired tid=" + Id);
                                    }
                                    mWaitingForSurface = false;
                                    Monitor.PulseAll(sGLThreadManager);
                                }

                                if (doRenderNotification)
                                {
                                    if (LOG_SURFACE)
                                    {
                                        Log.Info("GLThread", "sending render notification tid=" + Id);
                                    }
                                    wantRenderNotification = false;
                                    doRenderNotification = false;
                                    mRenderComplete = true;
                                    Monitor.PulseAll(sGLThreadManager);
                                }

                                // Ready to draw?
                                if (readyToDraw())
                                {
                                    // If we don't have an EGL context, try to acquire one.
                                    if (!mHaveEglContext)
                                    {
                                        if (askedToReleaseEglContext)
                                        {
                                            askedToReleaseEglContext = false;
                                        }
                                        else if (sGLThreadManager.tryAcquireEglContextLocked(this))
                                        {
                                            try
                                            {
                                                mEglHelper.start();
                                            }
                                            catch (Exception t)
                                            {
                                                sGLThreadManager.releaseEglContextLocked(this);
                                                throw t;
                                            }
                                            mHaveEglContext = true;
                                            createEglContext = true;

                                            Monitor.PulseAll(sGLThreadManager);
                                        }
                                    }

                                    if (mHaveEglContext && !mHaveEglSurface)
                                    {
                                        mHaveEglSurface = true;
                                        createEglSurface = true;
                                        createGlInterface = true;
                                        sizeChanged = true;
                                    }

                                    if (mHaveEglSurface)
                                    {
                                        if (mSizeChanged)
                                        {
                                            sizeChanged = true;
                                            w = mWidth;
                                            h = mHeight;
                                            wantRenderNotification = true;
                                            if (LOG_SURFACE)
                                            {
                                                Log.Info("GLThread", "noticing that we want render notification tid=" + Id);
                                            }
                                            // Destroy and recreate the EGL surface.
                                            createEglSurface = true;
                                            mSizeChanged = false;
                                        }
                                        mRequestRender = false;
                                        Monitor.PulseAll(sGLThreadManager);
                                        break;
                                    }
                                }

                                // By design, this is the only place in a GLThread thread where we Wait().
                                if (LOG_THREADS)
                                {
                                    Log.Info("GLThread", "Waiting tid=" + Id
                                        + " mHaveEglContext: " + mHaveEglContext
                                        + " mHaveEglSurface: " + mHaveEglSurface
                                        + " mFinishedCreatingEglSurface: " + mFinishedCreatingEglSurface
                                        + " mPaused: " + mPaused
                                        + " mHasSurface: " + mHasSurface
                                        + " mSurfaceIsBad: " + mSurfaceIsBad
                                        + " mWaitingForSurface: " + mWaitingForSurface
                                        + " mWidth: " + mWidth
                                        + " mHeight: " + mHeight
                                        + " mRequestRender: " + mRequestRender
                                        + " mRenderMode: " + mRenderMode);
                                }
                                Monitor.Wait(sGLThreadManager);
                            }
                        } // end of lock(sGLThreadManager)

                        if (ev != null)
                        {
                            ev.Run();
                            ev = null;
                            continue;
                        }

                        if (createEglSurface)
                        {
                            if (LOG_SURFACE)
                            {
                                Log.Debug("GLThread", "egl createSurface");
                            }
                            if (mEglHelper.createSurface())
                            {
                                lock (sGLThreadManager)
                                {
                                    mFinishedCreatingEglSurface = true;
                                    Monitor.PulseAll(sGLThreadManager);
                                }
                            }
                            else
                            {
                                lock (sGLThreadManager)
                                {
                                    mFinishedCreatingEglSurface = true;
                                    mSurfaceIsBad = true;
                                    Monitor.PulseAll(sGLThreadManager);
                                }
                                continue;
                            }
                            createEglSurface = false;
                        }

                        if (createGlInterface)
                        {
                            gl = mEglHelper.createGL().JavaCast<IGL10>();

                            sGLThreadManager.checkGLDriver(gl);
                            createGlInterface = false;
                        }

                        if (createEglContext)
                        {
                            if (LOG_RENDERER)
                            {
                                Log.Debug("GLThread", "onSurfaceCreated");
                            }
                            GLTextureView view = null;
                            if(mGLSurfaceViewWeakRef.TryGetTarget(out view))
                            {
                                view.mRenderer.OnSurfaceCreated(gl, mEglHelper.mEglConfig);
                            }
                            createEglContext = false;
                        }

                        if (sizeChanged)
                        {
                            if (LOG_RENDERER)
                            {
                                Log.Debug("GLThread", "onSurfaceChanged(" + w + ", " + h + ")");
                            }
                            GLTextureView view = null;
                            if (mGLSurfaceViewWeakRef.TryGetTarget(out view))
                            {
                                view.mRenderer.onSurfaceChanged(gl, w, h);
                            }
                            sizeChanged = false;
                        }

                        if (LOG_RENDERER_DRAW_FRAME)
                        {
                            Log.Debug("GLThread", "onDrawFrame tid=" + Id);
                        }
                        {
                            GLTextureView view = null;
                            if (mGLSurfaceViewWeakRef.TryGetTarget(out view))
                            {
                                view.mRenderer.onDrawFrame(gl);
                            }
                        }
                        int swapError = mEglHelper.swap();
                        switch (swapError)
                        {
                            case EGL10.EglSuccess:
                                break;
                            case EGL11.EglContextLost:
                                if (LOG_SURFACE)
                                {
                                    Log.Info("GLThread", "egl context lost tid=" + Id);
                                }
                                lostEglContext = true;
                                break;
                            default:
                                // Other errors typically mean that the current surface is bad,
                                // probably because the SurfaceView surface has been destroyed,
                                // but we haven't been notified yet.
                                // Log the error to help developers understand why com.pikkart.ar.rendering stopped.
                                EglHelper.logEglErrorAsWarning("GLThread", "eglSwapBuffers", swapError);

                                lock (sGLThreadManager)
                                {
                                    mSurfaceIsBad = true;
                                    Monitor.PulseAll(sGLThreadManager);
                                }
                                break;
                        }

                        if (wantRenderNotification)
                        {
                            doRenderNotification = true;
                        }
                    }

                }
                finally
                {
                    /*
                     * clean-up everything...
                     */
                    lock (sGLThreadManager)
                    {
                        stopEglSurfaceLocked();
                        stopEglContextLocked();
                    }
                }
            }

            public bool ableToDraw()
            {
                return mHaveEglContext && mHaveEglSurface && readyToDraw();
            }

            private bool readyToDraw()
            {
                return (!mPaused) && mHasSurface && (!mSurfaceIsBad) && (mWidth > 0) && (mHeight > 0) && (mRequestRender || (mRenderMode == RENDERMODE_CONTINUOUSLY));
            }

            public void setRenderMode(int renderMode)
            {
                if (!((RENDERMODE_WHEN_DIRTY <= renderMode) && (renderMode <= RENDERMODE_CONTINUOUSLY)))
                {
                    throw new Exception("renderMode");
                }
                lock (sGLThreadManager)
                {
                    mRenderMode = renderMode;
                    Monitor.PulseAll(sGLThreadManager);
                }
            }

            public int getRenderMode()
            {
                lock (sGLThreadManager)
                {
                    return mRenderMode;
                }
            }

            public void requestRender()
            {
                lock (sGLThreadManager)
                {
                    mRequestRender = true;
                    Monitor.PulseAll(sGLThreadManager);
                }
            }

            public void surfaceCreated()
            {
                lock (sGLThreadManager)
                {
                    if (LOG_THREADS)
                    {
                        Log.Info("GLThread", "surfaceCreated tid=" + Id);
                    }
                    mHasSurface = true;
                    mFinishedCreatingEglSurface = false;
                    Monitor.PulseAll(sGLThreadManager);
                    while (mWaitingForSurface && !mFinishedCreatingEglSurface && !mExited)
                    {
                        try
                        {
                            Monitor.Wait(sGLThreadManager);
                        }
                        catch (Exception e)
                        {
                            Thread.CurrentThread.Interrupt();
                        }
                    }
                }
            }


            public void surfaceDestroyed()
            {
                lock (sGLThreadManager)
                {
                    if (LOG_THREADS)
                    {
                        Log.Info("GLThread", "surfaceDestroyed tid=" + Id);
                    }
                    mHasSurface = false;
                    Monitor.PulseAll(sGLThreadManager);
                    while ((!mWaitingForSurface) && (!mExited))
                    {
                        try
                        {
                            Monitor.Wait(sGLThreadManager);
                        }
                        catch (Exception e)
                        {
                            Thread.CurrentThread.Interrupt();
                        }
                    }
                }
            }

            public void onPause()
            {
                lock (sGLThreadManager)
                {
                    if (LOG_PAUSE_RESUME)
                    {
                        Log.Info("GLThread", "onPause tid=" + Id);
                    }

                    mRequestPaused = true;
                    Monitor.PulseAll(sGLThreadManager);

                    while ((!mExited) && (!mPaused))
                    {
                        if (LOG_PAUSE_RESUME)
                        {
                            Log.Info("Main thread", "onPause Waiting for mPaused.");
                        }
                        try
                        {
                            Monitor.Wait(sGLThreadManager);
                        }
                        catch (Exception ex)
                        {
                            Thread.CurrentThread.Interrupt();
                        }
                    }
                }
            }

            public void onResume()
            {
                lock (sGLThreadManager)
                {
                    if (LOG_PAUSE_RESUME)
                    {
                        Log.Info("GLThread", "onResume tid=" + Id);
                    }
                    mRequestPaused = false;
                    mRequestRender = true;
                    mRenderComplete = false;
                    Monitor.PulseAll(sGLThreadManager);
                    while ((!mExited) && mPaused && (!mRenderComplete))
                    {
                        if (LOG_PAUSE_RESUME)
                        {
                            Log.Info("Main thread", "onResume Waiting for !mPaused.");
                        }
                        try
                        {
                            Monitor.Wait(sGLThreadManager);
                        }
                        catch (Exception ex)
                        {
                            Thread.CurrentThread.Interrupt();
                        }
                    }
                }
            }

            public void onWindowResize(int w, int h)
            {
                lock (sGLThreadManager)
                {
                    mWidth = w;
                    mHeight = h;
                    mSizeChanged = true;
                    mRequestRender = true;
                    mRenderComplete = false;
                    Monitor.PulseAll(sGLThreadManager);

                    // Wait for thread to react to resize and render a frame
                    while (!mExited && !mPaused && !mRenderComplete && ableToDraw())
                    {
                        if (LOG_SURFACE)
                        {
                            Log.Info("Main thread", "onWindowResize Waiting for render complete from tid=" + Id);
                        }
                        try
                        {
                            Monitor.Wait(sGLThreadManager);
                        }
                        catch (Exception ex)
                        {
                            Thread.CurrentThread.Interrupt();
                        }
                    }
                }
            }

            public void requestExitAndWait()
            {
                // don't call this from GLThread thread or it is a guaranteed deadlock!
                lock (sGLThreadManager)
                {
                    mShouldExit = true;
                    Monitor.PulseAll(sGLThreadManager);
                    while (!mExited)
                    {
                        try
                        {
                            Monitor.Wait(sGLThreadManager);
                        }
                        catch (Exception ex)
                        {
                            Thread.CurrentThread.Interrupt();
                        }
                    }
                }
            }

            public void requestReleaseEglContextLocked()
            {
                mShouldReleaseEglContext = true;
                Monitor.PulseAll(sGLThreadManager);
            }

            /**
             * Queue an "ev" to be run on the IGL com.pikkart.ar.rendering thread.
             * @param r the runnable to be run on the IGL com.pikkart.ar.rendering thread.
             */
            public void queueEvent(Java.Lang.Runnable r)
            {
                if (r == null)
                {
                    throw new Exception("r must not be null");
                }
                lock (sGLThreadManager)
                {
                    mEventQueue.Enqueue(r);
                    Monitor.PulseAll(sGLThreadManager);
                }
            }

            // Once the thread is started, all accesses to the following member
            // variables are protected by the sGLThreadManager monitor
            private bool mShouldExit;
            public bool mExited;
            private bool mRequestPaused;
            private bool mPaused;
            private bool mHasSurface;
            private bool mSurfaceIsBad;
            private bool mWaitingForSurface;
            private bool mHaveEglContext;
            private bool mHaveEglSurface;
            private bool mFinishedCreatingEglSurface;
            private bool mShouldReleaseEglContext;
            private int mWidth;
            private int mHeight;
            private int mRenderMode;
            private bool mRequestRender;
            private bool mRenderComplete;
            private Queue<Runnable> mEventQueue = new Queue<Runnable>();
            private bool mSizeChanged = true;

            // End of member variables protected by the sGLThreadManager monitor.

            private EglHelper mEglHelper;

            /**
             * Set once at thread construction time, nulled out when the parent view is garbage
             * called. This weak reference allows the GLSurfaceView to be garbage collected while
             * the GLThread is still alive.
             */
            private WeakReference<GLTextureView> mGLSurfaceViewWeakRef;

        }

        class LogWriter : Java.IO.Writer
        {
            public override void Close()
            {
                FlushBuilder();
            }
            override public void Flush()
            {
                FlushBuilder();
            }
            override public void Write(char[] buf, int offset, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    char c = buf[offset + i];
                    if (c == '\n')
                    {
                        FlushBuilder();
                    }
                    else
                    {
                        mBuilder.Append(c);
                    }
                }
            }

            private void FlushBuilder()
            {
                if (mBuilder.Length() > 0)
                {
                    Log.Verbose("GLSurfaceView", mBuilder.ToString());
                    mBuilder.Delete(0, mBuilder.Length());
                }
            }

            private StringBuilder mBuilder = new StringBuilder();
        }


        private void checkRenderThreadState()
        {
            if (mGLThread != null)
            {
                throw new Exception("setRenderer has already been called for this instance.");
            }
        }

        public void OnLayoutChange(View v, int left, int top, int right, int bottom, int oldLeft, int oldTop, int oldRight, int oldBottom)
        {
            OnSurfaceTextureSizeChanged(SurfaceTexture, right - left, bottom - top);
        }

        class GLThreadManager
        {
            private static string TAG = "GLThreadManager";
            GLTextureView Parent;
            public GLThreadManager(GLTextureView parent)
            {
                Parent = parent;
            }

            public void ThreadExiting(GLThread thread)
            {
                lock (this)
                {

                    if (LOG_THREADS)
                    {
                        //Log.Info("GLThread", "exiting tid=" + thread.Id);
                    }
                    thread.mExited = true;
                    if (mEglOwner == thread)
                    {
                        mEglOwner = null;
                    }
                    //Parent.NotifyAll();
                    Monitor.PulseAll(this);
                }
            }

            /*
             * Tries once to acquire the right to use an EGL
             * context. Does not block. Requires that we are already
             * in the sGLThreadManager monitor when this is called.
             *
             * @return true if the right to use an EGL context was acquired.
             */
            public bool tryAcquireEglContextLocked(GLThread thread)
            {
                if (mEglOwner == thread || mEglOwner == null)
                {
                    mEglOwner = thread;
                    //Parent.NotifyAll();
                    Monitor.PulseAll(this);
                    return true;
                }
                checkGLESVersion();
                if (mMultipleGLESContextsAllowed)
                {
                    return true;
                }
                // Notify the owning thread that it should release the context.
                if (mEglOwner != null)
                {
                    mEglOwner.requestReleaseEglContextLocked();
                }
                return false;
            }

            /*
             * Releases the EGL context. Requires that we are already in the
             * sGLThreadManager monitor when this is called.
             */
            public void releaseEglContextLocked(GLThread thread)
            {
                if (mEglOwner == thread)
                {
                    mEglOwner = null;
                }
                //Parent.NotifyAll();
                Monitor.PulseAll(this);
            }

            public bool shouldReleaseEGLContextWhenPausing()
            {
                lock (this)
                {
                    // Release the EGL context when pausing even if
                    // the hardware supports multiple EGL contexts.
                    // Otherwise the device could run out of EGL contexts.
                    return mLimitedGLESContexts;
                }
            }

            public bool shouldTerminateEGLWhenPausing()
            {
                lock (this)
                {
                    checkGLESVersion();
                    return !mMultipleGLESContextsAllowed;
                }
            }

            public void checkGLDriver(IGL10 gl)
            {
                lock (this)
                {
                    if (!mGLESDriverCheckComplete)
                    {
                        checkGLESVersion();
                        string renderer = gl.GlGetString(GL10.GlRenderer);
                        if (mGLESVersion < kGLES_20)
                        {
                            mMultipleGLESContextsAllowed = !renderer.StartsWith(kMSM7K_RENDERER_PREFIX);
                            //Parent.NotifyAll();
                            Monitor.PulseAll(this);
                        }
                        mLimitedGLESContexts = !mMultipleGLESContextsAllowed;
                        if (LOG_SURFACE)
                        {
                            Log.Debug(TAG, "checkGLDriver renderer = \"" + renderer + "\" multipleContextsAllowed = " + mMultipleGLESContextsAllowed + " mLimitedGLESContexts = " + mLimitedGLESContexts);
                        }
                        mGLESDriverCheckComplete = true;
                    }
                }
            }

            private void checkGLESVersion()
            {
                if (!mGLESVersionCheckComplete)
                {
                    mMultipleGLESContextsAllowed = true;
                    mGLESVersionCheckComplete = true;
                }
            }

            /**
             * This check was required for some pre-Android-3.0 hardware. Android 3.0 provides
             * support for hardware-accelerated views, therefore multiple EGL contexts are
             * supported on all Android 3.0+ EGL drivers.
             */
            private bool mGLESVersionCheckComplete;
            private int mGLESVersion;
            private bool mGLESDriverCheckComplete;
            private bool mMultipleGLESContextsAllowed;
            private bool mLimitedGLESContexts;
            private static int kGLES_20 = 0x20000;
            private static string kMSM7K_RENDERER_PREFIX = "Q3Dimension MSM7500 ";
            private GLThread mEglOwner;
        }        
    }

}