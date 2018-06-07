using Android.App;
using Android.Widget;
using Android.OS;
using Com.Pikkart.AR.Geo;
using System.Collections.Generic;
using Android.Support.V4.App;
using Android.Content.PM;
using Android.Runtime;
using Android.Locations;
using Android.Content;
using Android.Views;
using Android.Support.V7.App;

namespace TutorialApp
{
    [Activity(Label = "TutorialApp", MainLauncher = true, Icon = "@drawable/icon", ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation, Theme = "@style/AppTheme")]
    public class MainActivity : AppCompatActivity, IArGeoListener
    {
        private ARView m_arView;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            //if not Android 6+ run the app
            if (Build.VERSION.SdkInt < BuildVersionCodes.M)
            {
                InitLayout();
            }
            else
            {
                CheckPermissions(m_permissionCode);
            }
        }

        protected override void OnResume()
        {
            base.OnResume();
            if (m_arView != null) m_arView.onResume();
        }

        protected override void OnPause()
        {
            base.OnPause();
            //pause our renderer and associated videos
            if (m_arView != null) m_arView.onPause();
        }

        private void InitLayout()
        {
            SetContentView(Resource.Layout.Main);

            GeoFragment m_geoFragment = ((GeoFragment)FragmentManager.FindFragmentById(Resource.Id.geo_fragment));
            m_geoFragment.DisableRecognition();
            m_geoFragment.SetGeoListener(this);

            Location loc1 = new Location("loc1");
            loc1.Latitude = 44.654894;
            loc1.Longitude = 10.914749;

            Location loc2 = new Location("loc2");
            loc2.Latitude = 44.653505;
            loc2.Longitude = 10.909653;

            Location loc3 = new Location("loc3");
            loc3.Latitude = 44.647315;
            loc3.Longitude = 10.924802;

            List<GeoElement> geoElementList = new List<GeoElement>();
            geoElementList.Add(new GeoElement(loc1, "1", "COOP, Modena")); 
            geoElementList.Add(new GeoElement(loc2, "2", "Burger King, Modena"));
            geoElementList.Add(new GeoElement(loc3, "3", "Piazza Matteotti, Modena"));

            m_geoFragment.SetGeoElements(geoElementList);

            RelativeLayout rl = (RelativeLayout)FindViewById(Resource.Id.ar_main_layout);
            m_arView = new ARView(this);
            rl.AddView(m_arView, new FrameLayout.LayoutParams(FrameLayout.LayoutParams.MatchParent, FrameLayout.LayoutParams.MatchParent));

            m_geoFragment.SetCameraTextureView(m_arView);

            m_geoFragment.SetMarkerViewAdapters(new MyMarkerViewAdapter(this, 51, 73), new MyMarkerViewAdapter(this, 30, 43));
        }

        public void OnGeoElementClicked(GeoElement p0)
        {
            Toast.MakeText(this, p0.Name + " is there", ToastLength.Short).Show();
        }

        public void OnMapOrCameraClicked()
        {

        }

        public void OnGeolocationChanged(Location p0)
        {

        }

        public void OnGeoBringInterfaceOnTop()
        {

        }

        private int m_permissionCode = 1234;
        private void CheckPermissions(int code)
        {
            string[] permissions_required = new string[] {
                Android.Manifest.Permission.Camera,
                Android.Manifest.Permission.WriteExternalStorage,
                Android.Manifest.Permission.ReadExternalStorage,
                Android.Manifest.Permission.AccessNetworkState,
                Android.Manifest.Permission.AccessCoarseLocation,
                Android.Manifest.Permission.AccessFineLocation};

            List<string> permissions_not_granted_list = new List<string>();
            foreach (string permission in permissions_required)
            {
                if (ActivityCompat.CheckSelfPermission(this, permission) != Android.Content.PM.Permission.Granted)
                {
                    permissions_not_granted_list.Add(permission);
                }
            }
            if (permissions_not_granted_list.Count > 0)
            {
                string[] permissions = new string[permissions_not_granted_list.Count];
                permissions = permissions_not_granted_list.ToArray();
                ActivityCompat.RequestPermissions(this, permissions, m_permissionCode);
            }
            else
            {
                InitLayout();
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            if (requestCode == m_permissionCode)
            {
                bool isGranted = true;
                for (int i = 0; i < grantResults.Length; ++i)
                {
                    isGranted = isGranted && (grantResults[i] == Permission.Granted);
                }
                if (isGranted)
                {
                    InitLayout();
                }
                else
                {
                    Toast.MakeText(this, "Error: required permissions not granted!", ToastLength.Short).Show();
                    Finish();
                }
            }
        }

        private class MyMarkerViewAdapter : MarkerViewAdapter
        {
            Context _context;
            public MyMarkerViewAdapter(Context context, int width, int height) : base(context, width, height)
            {
                IsDefaultMarker = true;
                _context = context;
            }

            public override View GetView(GeoElement p0)
            {
                ImageView imageView = (ImageView)MarkerView.FindViewById(Resource.Id.image);
                imageView.SetImageResource(Resource.Drawable.map_marker_yellow);
                imageView.Invalidate();
                return MarkerView;
            }

            public override View GetSelectedView(GeoElement p0)
            {
                ImageView imageView = (ImageView)MarkerView.FindViewById(Resource.Id.image);
                imageView.SetImageResource(Resource.Drawable.map_marker_dark_green);
                imageView.Invalidate();
                return MarkerView;
            }
        }
    }
}

