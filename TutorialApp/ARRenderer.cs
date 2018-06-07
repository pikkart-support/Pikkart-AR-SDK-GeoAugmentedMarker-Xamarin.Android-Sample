using Android.Content;
using Javax.Microedition.Khronos.Egl;
using Javax.Microedition.Khronos.Opengles;
using Com.Pikkart.AR.Geo;
using Com.Pikkart.AR.Recognition;

namespace TutorialApp
{
    public class ARRenderer : GLTextureView.Renderer
    {
        public bool IsActive = false;
        //the rendering viewport dimensions
        private int ViewportWidth;
        private int ViewportHeight;
        //normalized screen orientation (0=landscale, 90=portrait, 180=inverse landscale, 270=inverse portrait)
        private int Angle;
        //
        private Context context;

        /* Constructor. */
        public ARRenderer(Context con)
        {
            context = con;
        }

        /** Called to draw the current frame. */
        public void onDrawFrame(IGL10 gl)
        {
            if (!IsActive) return;

            gl.GlClear(GL10.GlColorBufferBit | GL10.GlDepthBufferBit);

            // Call our native function to render camera content
            GeoFragment.RenderCamera(ViewportWidth, ViewportHeight, Angle);

            gl.GlFinish();
        }

        /** Called when the surface changed size. */
        public void onSurfaceChanged(IGL10 gl, int width, int height)
        {

        }

        /** Called when the surface is created or recreated.
         * Reinitialize OpenGL related stuff here*/
        public void OnSurfaceCreated(IGL10 gl, EGLConfig config)
        {
            gl.GlClearColor(1.0f, 1.0f, 1.0f, 1.0f);
        }

        /** Called when the surface is destroyed. */
        public void onSurfaceDestroyed()
        {

        }

        /* this will be called by our GLTextureView-derived class to update screen sizes and orientation */
        public void UpdateViewport(int viewportWidth, int viewportHeight, int angle, int geoAngle)
        {
            ViewportWidth = viewportWidth;
            ViewportHeight = viewportHeight;
            Angle = angle;
            GeoNativeWrapper.UpdateProjectionCamera(ARNativeWrapper.CameraWidth(), ARNativeWrapper.CameraHeight(), ViewportWidth, ViewportHeight, geoAngle);
        }
    }
}