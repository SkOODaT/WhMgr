﻿namespace WhMgr.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Twilio;
    using Twilio.Rest.Api.V2010.Account;

    using WhMgr.Configuration;
    using WhMgr.Diagnostics;
    using WhMgr.Geofence;
    using WhMgr.Net.Models;
    using WhMgr.Osm;
    using WhMgr.Osm.Models;

    public static class Utils
    {
        private static readonly IEventLogger _logger = EventLogger.GetLogger("UTILS", Program.LogLevel);

        // TODO: Provide better way for replacement values
        public static string GetStaticMapsUrl(string templateFileName, string staticMapUrl, int staticMapZoom, double latitude, double longitude, string markerImageUrl, PokemonTeam? team, OsmFeature feature = null, MultiPolygon multiPolygon = null)
        {
            var staticMapData = Renderer.Parse(templateFileName, new
            {
                lat = latitude,
                lon = longitude,
                team = team?.ToString(),
                team_id = Convert.ToInt32(team ?? 0),
                marker = markerImageUrl,
                pkmn_img_url = markerImageUrl,
                quest_reward_img_url = markerImageUrl,
                weather_img_url = markerImageUrl,
                tilemaps_url = staticMapUrl
            }); ;
            StaticMapConfig staticMap = JsonConvert.DeserializeObject<StaticMapConfig>(staticMapData);

            var url = string.Format(staticMapUrl, latitude, longitude, staticMapZoom);
            var markerUrl = staticMap.Markers.Count > 0 ? url + "?markers=" + Uri.EscapeDataString(JsonConvert.SerializeObject(staticMap.Markers)) : string.Empty;

            if (feature != null)
            {
                var latlng = OsmManager.MultiPolygonToLatLng(feature.Geometry?.Coordinates);
                var polygonKey = "&polygons=";
                var polygonUrl = @"[{""fill_color"":""rgba(100.0%,0.0%,0.0%,0.5)"",""stroke_color"":""black"",""stroke_width"":1,""path"":" + latlng + "}]";
                markerUrl += polygonKey + Uri.EscapeDataString(polygonUrl);
            }

            if (multiPolygon != null)
            {
                var latlng = OsmManager.MultiPolygonToLatLng(new List<MultiPolygon> { multiPolygon });
                var polygonKey = "&polygons=";
                var polygonUrl = @"[{""fill_color"":""rgba(100.0%,0.0%,0.0%,0.5)"",""stroke_color"":""black"",""stroke_width"":1,""path"":" + latlng + "}]";
                markerUrl += polygonKey + Uri.EscapeDataString(polygonUrl);
            }

            // ***** FLO HACK *****
            var FloPath = staticMapUrl + markerImageUrl;
            // ***** FLO HACK *****
            //_logger.Trace($"{FloPath}");

            return FloPath;
        }

        public static bool SendSmsMessage(string body, TwilioConfig config, string toPhoneNumber)
        {
            if (!config.Enabled)
            {
                // Twilio text message notifications not enabled
                return false;
            }

            TwilioClient.Init(config.AccountSid, config.AuthToken);
            var message = MessageResource.Create(
                body: body,
                from: new Twilio.Types.PhoneNumber($"+1{config.FromNumber}"),
                to: new Twilio.Types.PhoneNumber($"+1{toPhoneNumber}")
            );
            //Console.WriteLine($"Response: {message}");
            return message.ErrorCode == null;
        }

        public static Location GetGoogleAddress(string city, double lat, double lng, string gmapsKey)
        {
            // ***** OSM *****
            var url = $"https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat={lat}&lon={lng}";
            // ***** OSM *****
            //var apiKey = string.IsNullOrEmpty(gmapsKey) ? string.Empty : $"&key={gmapsKey}";
            //var url = $"https://maps.googleapis.com/maps/api/geocode/json?latlng={lat},{lng}&sensor=true{apiKey}";
            var unknown = "Unknown";
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.UserAgent = "[Whmgr User-Agent]";
                var response = request.GetResponse();
                using (var responseStream = response.GetResponseStream())
                {
                    var reader = new StreamReader(responseStream, Encoding.UTF8);
                    var data = reader.ReadToEnd();

                    var parseJson = JObject.Parse(data);
                    // ***** OSM *****
                    //_logger.Trace($"{parseJson}");
                    var osmhouse_number = Convert.ToString(parseJson["address"]["house_number"]);
                    var osmroad = Convert.ToString(parseJson["address"]["road"]);
                    var osmcity = Convert.ToString(parseJson["address"]["city"]);
                    var osmneighbourhood = " **[" + Convert.ToString(parseJson["address"]["neighbourhood"])+ "]** ";
                    var jsonToString = osmneighbourhood  + osmhouse_number + " " + osmroad + ", " + osmcity;
                    var address = Convert.ToString(jsonToString);
                    // ***** OSM *****
                    //_logger.Trace($"{address}");
                    //var status = Convert.ToString(parseJson["status"]);
                    //if (string.Compare(status, "OK", true) != 0)
                    //    return null;
                    //var result = parseJson["results"].FirstOrDefault();
                    //var address = Convert.ToString(result["formatted_address"]);
                    //var area = Convert.ToString(result["address_components"][2]["long_name"]);
                    return new Location(address, city ?? unknown, lat, lng);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
            return null;
        }
    }
}