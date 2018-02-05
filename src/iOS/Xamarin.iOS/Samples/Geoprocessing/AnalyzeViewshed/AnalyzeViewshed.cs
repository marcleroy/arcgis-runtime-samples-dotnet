﻿// Copyright 2017 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks;
using Esri.ArcGISRuntime.Tasks.Geoprocessing;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using Foundation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UIKit;

namespace ArcGISRuntimeXamarin.Samples.AnalyzeViewshed
{
    [Register("AnalyzeViewshed")]
    public class AnalyzeViewshed : UIViewController
    {
        // Constant holding offset where the MapView control should start
        private const int yPageOffset = 60;

        // Create and hold reference to the used MapView
        private MapView _myMapView = new MapView();

        // Url for the geoprocessing service
        private const string _viewshedUrl =
            "https://sampleserver6.arcgisonline.com/arcgis/rest/services/Elevation/ESRI_Elevation_World/GPServer/Viewshed";

        // The graphics overlay to show where the user clicked in the map
        private GraphicsOverlay _inputOverlay;

        // The graphics overlay to display the result of the viewshed analysis
        private GraphicsOverlay _resultOverlay;

        public AnalyzeViewshed()
        {
            Title = "Viewshed (Geoprocessing)";
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            // Create the UI, setup the control references and execute initialization
            CreateLayout();
            Initialize();
        }

        public override void ViewDidLayoutSubviews()
        {
            // Setup the visual frame for the MapView
            _myMapView.Frame = new CoreGraphics.CGRect(0, 0, View.Bounds.Width, View.Bounds.Height);
        }

        private void Initialize()
        {
            // Create a map with topographic basemap and an initial location
            Map myMap = new Map(BasemapType.Topographic, 45.3790902612337, 6.84905317262762, 13);

            // Hook into the MapView tapped event
            _myMapView.GeoViewTapped += MyMapView_GeoViewTapped;

            // Create empty overlays for the user clicked location and the results of the viewshed analysis
            CreateOverlays();

            // Assign the map to the MapView
            _myMapView.Map = myMap;
        }

        private async void MyMapView_GeoViewTapped(object sender, GeoViewInputEventArgs e)
        {
            // Clear previous user click location and the viewshed geoprocessing task results
            _inputOverlay.Graphics.Clear();
            _resultOverlay.Graphics.Clear();

            // Get the tapped point
            MapPoint geometry = e.Location;

            // Create a marker graphic where the user clicked on the map and add it to the existing graphics overlay
            Graphic myInputGraphic = new Graphic(geometry);
            _inputOverlay.Graphics.Add(myInputGraphic);

            // Normalize the geometry if wrap-around is enabled
            //    This is necessary because of how wrapped-around map coordinates are handled by Runtime
            //    Without this step, the task may fail because wrapped-around coordinates are out of bounds.
            if (_myMapView.IsWrapAroundEnabled) { geometry = GeometryEngine.NormalizeCentralMeridian(geometry) as MapPoint; }

            // Execute the geoprocessing task using the user click location
            await CalculateViewshed(geometry);
        }

        private async Task CalculateViewshed(MapPoint location)
        {
            // This function will define a new geoprocessing task that performs a custom viewshed analysis based upon a
            // user click on the map and then display the results back as a polygon fill graphics overlay. If there
            // is a problem with the execution of the geoprocessing task an error message will be displayed

            // Create new geoprocessing task using the url defined in the member variables section
            var myViewshedTask = await GeoprocessingTask.CreateAsync(new Uri(_viewshedUrl));

            // Create a new feature collection table based upon point geometries using the current map view spatial reference
            var myInputFeatures = new FeatureCollectionTable(new List<Field>(), GeometryType.Point, _myMapView.SpatialReference);

            // Create a new feature from the feature collection table. It will not have a coordinate location (x,y) yet
            Feature myInputFeature = myInputFeatures.CreateFeature();

            // Assign a physical location to the new point feature based upon where the user clicked in the map view
            myInputFeature.Geometry = location;

            // Add the new feature with (x,y) location to the feature collection table
            await myInputFeatures.AddFeatureAsync(myInputFeature);

            // Create the parameters that are passed to the used geoprocessing task
            GeoprocessingParameters myViewshedParameters =
                new GeoprocessingParameters(GeoprocessingExecutionType.SynchronousExecute);

            // Request the output features to use the same SpatialReference as the map view
            myViewshedParameters.OutputSpatialReference = _myMapView.SpatialReference;

            // Add an input location to the geoprocessing parameters
            myViewshedParameters.Inputs.Add("Input_Observation_Point", new GeoprocessingFeatures(myInputFeatures));

            // Create the job that handles the communication between the application and the geoprocessing task
            var myViewshedJob = myViewshedTask.CreateJob(myViewshedParameters);

            try
            {
                // Execute analysis and wait for the results
                GeoprocessingResult myAnalysisResult = await myViewshedJob.GetResultAsync();

                // Get the results from the outputs
                GeoprocessingFeatures myViewshedResultFeatures = myAnalysisResult.Outputs["Viewshed_Result"] as GeoprocessingFeatures;

                // Add all the results as a graphics to the map
                IFeatureSet myViewshedAreas = myViewshedResultFeatures.Features;
                foreach (var myFeature in myViewshedAreas)
                {
                    _resultOverlay.Graphics.Add(new Graphic(myFeature.Geometry));
                }
            }
            catch (Exception ex)
            {
                // Display an error message if there is a problem
                if (myViewshedJob.Status == JobStatus.Failed && myViewshedJob.Error != null)
                {
                    // Report error
                    UIAlertController alert = UIAlertController.Create("Geoprocessing Error", myViewshedJob.Error.Message, UIAlertControllerStyle.Alert);
                    alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                    PresentViewController(alert, true, null);
                }
                else
                {
                    // Report error
                    UIAlertController alert = UIAlertController.Create("Sample Error", ex.ToString(), UIAlertControllerStyle.Alert);
                    alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                    PresentViewController(alert, true, null);
                }
            }
        }

        private void CreateOverlays()
        {
            // This function will create the overlays that show the user clicked location and the results of the
            // viewshed analysis. Note: the overlays will not be populated with any graphics at this point

            // Create renderer for input graphic. Set the size and color properties for the simple renderer
            SimpleRenderer myInputRenderer = new SimpleRenderer()
            {
                Symbol = new SimpleMarkerSymbol()
                {
                    Size = 15,
                    Color = System.Drawing.Color.Red
                }
            };

            // Create overlay to where input graphic is shown
            _inputOverlay = new GraphicsOverlay()
            {
                Renderer = myInputRenderer
            };

            // Create fill renderer for output of the viewshed analysis. Set the color property of the simple renderer
            SimpleRenderer myResultRenderer = new SimpleRenderer()
            {
                Symbol = new SimpleFillSymbol()
                {
                    Color = System.Drawing.Color.FromArgb(100, 226, 119, 40)
                }
            };

            // Create overlay to where viewshed analysis graphic is shown
            _resultOverlay = new GraphicsOverlay()
            {
                Renderer = myResultRenderer
            };

            // Add the created overlays to the MapView
            _myMapView.GraphicsOverlays.Add(_inputOverlay);
            _myMapView.GraphicsOverlays.Add(_resultOverlay);
        }

        private void CreateLayout()
        {
            // Add MapView to the page
            View.AddSubviews(_myMapView);
        }
    }
}