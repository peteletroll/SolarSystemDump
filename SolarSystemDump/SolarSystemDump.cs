using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MiniJSON;
using UnityEngine;

namespace SolarSystemDump
{
	[KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
	public class SolarSystemDump: MonoBehaviour
	{
		const double DEG2RAD = Math.PI / 180.0;
		const double RAD2DEG = 180.0 / Math.PI;
		const double G0 = 9.81;

		public void Awake()
		{
			log("Awake() called in " + HighLogic.LoadedScene);
		}

		public void Start()
		{
			log("Start() called in " + HighLogic.LoadedScene);
			dumpJson();
			enabled = false;
		}

		public void OnDestroy()
		{
			log("OnDestroy() called in " + HighLogic.LoadedScene);
		}

		static bool dumpedJson = false;

		public class JsonArray: List<object> { }

		public class JsonObject: Dictionary<string, object> { }

		public void dumpJson()
		{
			if (dumpedJson)
				return;

			StreamWriter stream = null;

			try {
				string json = Json.Serialize(systemJson(), true, "  ");
				string assembly = Assembly.GetExecutingAssembly().Location;
				string directory = Path.GetDirectoryName(assembly);
				string file = Path.Combine(directory, nameof(SolarSystemDump) + ".json");
				log("dumping to " + file);
				stream = new StreamWriter(file);
				// log("dumping: " + json);
				stream.Write(json);
				stream.Write('\n');
				dumpedJson = true;
			} catch (Exception e) {
				log("can't save: " + e.Message + "\n" + e.StackTrace);
			} finally {
				if (stream != null)
					stream.Close();
			}
		}

		public static JsonObject systemJson()
		{
			JsonObject json = new JsonObject();
			json.Add("version", Versioning.VersionString);
			json.Add("timeUnits", timeUnitsJson());
			CelestialBody rootBody = null;

			JsonObject bodies = bodiesJson(ref rootBody);
			if (rootBody)
				json.Add("rootBody", rootBody.name);
			json.Add("bodies", bodies);

			return json;
		}

		public static JsonObject timeUnitsJson()
		{
			IDateTimeFormatter dtf = KSPUtil.dateTimeFormatter;
			if (dtf == null)
				return null;
			JsonObject json = new JsonObject();
			json.Add("Year", dtf.Year);
			json.Add("Day", dtf.Day);
			json.Add("Hour", dtf.Hour);
			json.Add("Minute", dtf.Minute);
			return json;
		}

		public static JsonObject bodiesJson(ref CelestialBody rootBody)
		{
			rootBody = null;
			JsonObject json = new JsonObject();
			if (FlightGlobals.Bodies != null) {
				for (int i = 0; i < FlightGlobals.Bodies.Count; i++) {
					CelestialBody body = FlightGlobals.Bodies[i];
					if (body == null || body.name == null)
						continue;
					if (body.orbit == null)
						rootBody = body;
					json.Add(body.name, bodyJson(body, i));
				}
			}
			return json;
		}

		public static JsonObject bodyJson(CelestialBody body, int index)
		{
			JsonObject json = new JsonObject();
			if (body == null)
				return json;

			JsonObject info = new JsonObject();
			json.Add("info", info);
			info.Add("index", index);
			info.Add("name", body.name);
			info.Add("isStar", body.isStar);
			info.Add("isHomeWorld", body.isHomeWorld);
			info.Add("timeWarpAltitudeLimits", toJson(body.timeWarpAltitudeLimits));
			info.Add("orbitingBodies", orbitingBodies(body));

			JsonObject size = new JsonObject();
			json.Add("size", size);
			size.Add("radius", body.Radius);
			size.Add("maxHeight", body.pqsController ? body.pqsController.mapMaxHeight : 0.0);
			size.Add("mass", body.Mass);
			size.Add("mu", body.gravParameter);
			size.Add("GeeASL", body.GeeASL);
			size.Add("g0", G0 * body.GeeASL);
			size.Add("sphereOfInfluence", body.sphereOfInfluence);
			size.Add("hillSphere", body.hillSphere);
			size.Add("oceanDensity", body.oceanDensity);

			JsonObject surface = new JsonObject();
			json.Add("surface", surface);
			surface.Add("hasSolidSurface", body.hasSolidSurface);
			surface.Add("ocean", body.ocean);
			surface.Add("albedo", body.albedo);
			surface.Add("emissivity", body.emissivity);

			if (body.atmosphere) {
				JsonObject atmosphere = new JsonObject();
				json.Add("atmosphere", atmosphere);
				atmosphere.Add("atmosphereDepth", body.atmosphereDepth);
				atmosphere.Add("atmosphereContainsOxygen", body.atmosphereContainsOxygen);
			}

			JsonObject rotation = new JsonObject();
			json.Add("rotation", rotation);
			rotation.Add("axis", toJson(body.RotationAxis));
			rotation.Add("solarDayLength", body.solarDayLength);
			rotation.Add("rotationPeriod", body.rotationPeriod);
			rotation.Add("solarRotationPeriod", body.solarRotationPeriod);
			rotation.Add("rotates", body.rotates);
			rotation.Add("tidallyLocked", body.tidallyLocked);
			rotation.Add("initialRotationRad", DEG2RAD * body.initialRotation);
			rotation.Add("initialRotationDeg", body.initialRotation);

			json.Add("orbit", orbitJson(body));

			JsonObject science = new JsonObject();
			json.Add("science", science);
			if (body.scienceValues != null) {
				CelestialBodyScienceParams sv = body.scienceValues;
				science.Add("flyingAltitudeThreshold", sv.flyingAltitudeThreshold);
				science.Add("spaceAltitudeThreshold", sv.spaceAltitudeThreshold);

				science.Add("InSpaceHighDataValue", sv.InSpaceHighDataValue);
				science.Add("InSpaceLowDataValue", sv.InSpaceLowDataValue);
				science.Add("FlyingLowDataValue", sv.FlyingLowDataValue);
				science.Add("FlyingHighDataValue", sv.FlyingHighDataValue);
				science.Add("LandedDataValue", sv.LandedDataValue);
				science.Add("SplashedDataValue", sv.SplashedDataValue);
				science.Add("RecoveryValue", sv.RecoveryValue);
			}
			science.Add("biomes", toJson(ResearchAndDevelopment.GetBiomeTags(body, false)));
			science.Add("miniBiomes", toJson(ResearchAndDevelopment.GetMiniBiomeTags(body)));

			JsonArray anomalies = new JsonArray();
			PQSSurfaceObject[] aa = body.pqsSurfaceObjects;
			if (aa != null) {
				for (int i = 0; i < aa.Length; i++) {
					PQSSurfaceObject a = aa[i];
					if (a != null && a.name != "Randolith") {
						JsonObject j = new JsonObject();
						j.Add("name", a.name);
						j.Add("objectName", a.SurfaceObjectName);
						Vector3d p = a.PlanetRelativePosition;
						j.Add("lat", Mathf.Rad2Deg * Mathf.Asin((float) p.normalized.y));
						j.Add("lon", Mathf.Rad2Deg * Math.Atan2(p.z, p.x));
						anomalies.Add(j);
					}
				}
			}
			json.Add("anomalies", anomalies);

			JsonObject roc = rocJson(body.name);
			if (roc != null)
				json.Add("roc", roc);

			return json;
		}

		public static JsonArray orbitingBodies(CelestialBody body)
		{
			JsonArray json = new JsonArray();
			if (body.orbitingBodies != null && body.orbitingBodies.Count > 0) {
				int childrenCount = body.orbitingBodies.Count;
				// log(body.name + " has " + childrenCount + " children");
				for (int i = 0; i < childrenCount; i++) {
					// log("child " + i);
					if (body.orbitingBodies[i] != null)
						json.Add(body.orbitingBodies[i].name);
				}
			}
			return json;
		}

		public static JsonObject orbitJson(CelestialBody body)
		{
			if (body == null || body.orbit == null)
				return null;
			Orbit orbit = body.orbit;
			JsonObject json = new JsonObject();
			json.Add("referenceBody", orbit.referenceBody != null ? orbit.referenceBody.name : null);
			json.Add("period", orbit.period);
			json.Add("semiMajorAxis", orbit.semiMajorAxis);
			json.Add("semiLatusRectum", orbit.semiLatusRectum);
			json.Add("eccentricity", orbit.eccentricity);
			json.Add("inclinationRad", DEG2RAD * orbit.inclination);
			json.Add("inclinationDeg", orbit.inclination);
			json.Add("longitudeOfAscendingNodeRad", DEG2RAD * orbit.LAN);
			json.Add("longitudeOfAscendingNodeDeg", orbit.LAN);
			json.Add("argumentOfPeriapsisRad", DEG2RAD * orbit.argumentOfPeriapsis);
			json.Add("argumentOfPeriapsisDeg", orbit.argumentOfPeriapsis);
			json.Add("meanAnomalyAtEpochRad", orbit.meanAnomalyAtEpoch);
			json.Add("meanAnomalyAtEpochDeg", RAD2DEG * orbit.meanAnomalyAtEpoch);
			json.Add("normal", toJson(orbit.GetOrbitNormal()));
			return json;
		}

		public static JsonObject rocJson(string body)
		{
			List<ROCDefinition> roc = ROCManager.Instance?.rocDefinitions;
			if (roc == null)
				return null;

			JsonObject ret = new JsonObject();
			roc.ForEach(r => {
				// log("ROC TYPE " + r.type);
				string rocname = r.type;
				if (r.myCelestialBodies != null) {
					// log("ROC BODIES " + r.myCelestialBodies.Count);
					r.myCelestialBodies.ForEach(d => {
						// log("ROC BODY " + d.name + " ON " + body);
						if (d.name == body && d.biomes != null) {
							JsonArray biomes = new JsonArray();
							d.biomes.ForEach(b => biomes.Add(b.Replace(" ", "")));
							ret.Add(rocname, biomes);
						}
					});
				}
			});

			return ret;
		}

		public static JsonArray toJson(Vector3d v)
		{
			if (v == null)
				return null;
			JsonArray ret = new JsonArray();
			ret.Add(v.x);
			ret.Add(v.y);
			ret.Add(v.z);
			return ret;
		}

		public static JsonArray toJson(float[] v)
		{
			JsonArray ret = new JsonArray();
			for (int i = 0; i < v.Length; i++)
				ret.Add(v[i]);
			return ret;
		}

		public static JsonArray toJson(List<string> l)
		{
			if (l == null)
				return null;
			JsonArray ret = new JsonArray();
			for (int i = 0; i < l.Count; i++)
				ret.Add(l[i]);
			return ret;
		}

		public static void log(string msg)
		{
			print(nameof(SolarSystemDump) + ": " + msg);
		}
	}
}
