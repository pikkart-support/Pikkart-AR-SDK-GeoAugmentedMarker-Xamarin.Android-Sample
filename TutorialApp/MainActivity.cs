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

namespace TutorialApp
{
    [Activity(Label = "TutorialApp", MainLauncher = true, Icon = "@drawable/icon", ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation, Theme = "@style/AppTheme")]
    public class MainActivity : GeoActivity
    {
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            //if not Android 6+ run the app
            if (Build.VERSION.SdkInt < BuildVersionCodes.M)
            {
                Init();
            }
            else
            {
                CheckPermissions(m_permissionCode);
            }
        }

        private void Init()
        {
            MyMarkerViewAdapter arMyMarkerViewAdapter = new MyMarkerViewAdapter(this, 51, 73);
            MyMarkerViewAdapter mapMyMarkerViewAdapter = new MyMarkerViewAdapter(this, 30, 43);

            //InitGeoFragment(arMyMarkerViewAdapter, mapMyMarkerViewAdapter);
            InitGeoFragment();
            Location loc1 = new Location("loc1");
            loc1.Latitude = 45.466019;
            loc1.Longitude = 9.188020;


            Location loc2 = new Location("loc2");
            loc2.Latitude = 41.903598;
            loc2.Longitude = 12.476896;


            Location loc3 = new Location("loc3");
            loc3.Latitude = 44.647225;
            loc3.Longitude = 10.924819;

            List<GeoElement> geoElementList = new List<GeoElement>();
            geoElementList.Add(new GeoElement(loc1, "1", "Scala, Milano"));
            geoElementList.Add(new GeoElement(loc2, "2", "Rione II Trevi, Roma"));
            geoElementList.Add(new GeoElement(loc3, "3", "Modena, Circoscrizione 1"));
            SetGeoElements(geoElementList);
        }

        public override void OnGeoElementClicked(GeoElement p0)
        {
            Toast.MakeText(this, p0.Name + " is there", ToastLength.Short).Show();
        }

        public override void OnMapOrCameraClicked()
        {
            base.OnMapOrCameraClicked();
        }

        public override void OnGeolocationChanged(Location p0)
        {
            base.OnGeolocationChanged(p0);
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
                Init();
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
                    Init();
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
                imageView.SetImageResource(Resource.Drawable.map_marker_blue);
                imageView.Invalidate();
                return MarkerView;
            }
        }
    }
}

