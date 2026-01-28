using System;
using System.Text;
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

		public void Awake()
		{
			log("Awake() called in " + HighLogic.LoadedScene);
		}

		public void Start()
		{
			log("Start() called in " + HighLogic.LoadedScene);
			dumpJson();
			enabled = false;
			setEvents(true);
		}

		public void OnDestroy()
		{
			log("OnDestroy() called in " + HighLogic.LoadedScene);
			setEvents(false);
		}

		bool eventState = false;

		private void setEvents(bool newState)
		{
			if (newState == eventState)
				return;
			eventState = newState;
			if (newState) {
				GameEvents.onVesselGoOffRails.Add(offRails);
			} else {
				GameEvents.onVesselGoOffRails.Remove(offRails);
			}
		}

		void offRails(Vessel v)
		{
			dumpJson();
		}

		public class JsonArray: List<object> { }

		public class JsonObject: Dictionary<string, object> { }

		public class AnomalyCollector {
			private HashSet<PQSSurfaceObject> visited = new HashSet<PQSSurfaceObject>();

			private JsonArray anomalies = new JsonArray();

			private bool addPQSJson(PQSSurfaceObject so, CelestialBody body)
			{
				if (so == null)
					return false;
				Vector3d p = so.PlanetRelativePosition;
				float lat = Mathf.Rad2Deg * Mathf.Asin((float)p.normalized.y);
				float lon = Mathf.Rad2Deg * Mathf.Atan2((float)p.z, (float)p.x);
				if (so.name == "Randolith") {
					if (Mathf.Abs(lat - -28.80831f) < 1e-3f && Mathf.Abs(lon - -13.44011f) < 1e-3f)
						return false;
					log("found " + so.name + " on " + body.name);
				}
				JsonObject j = new JsonObject();
				j.Add("name", so.name);
				j.Add("objectName", so.SurfaceObjectName);
				j.Add("lat", lat);
				j.Add("lon", lon);
				j.Add("class", so.GetType().ToString());
				anomalies.Add(j);
				return true;
			}

			public bool visit(PQSCity pc, CelestialBody body)
			{
				if (pc == null)
					return false;
				if (visited.Contains(pc))
					return false;
				visited.Add(pc);
				addPQSJson(pc, body);
				return true;
			}

			public bool visit(PQSCity2 pc, CelestialBody body)
			{
				if (pc == null)
					return false;
				if (visited.Contains(pc))
					return false;
				visited.Add(pc);
				addPQSJson(pc, body);
				return true;
			}

			public bool visit(PQSSurfaceObject so, CelestialBody body)
			{
				if (so == null)
					return false;
				if (so is PQSCity)
					return visit(so as PQSCity, body);
				if (so is PQSCity2)
					return visit(so as PQSCity2, body);
				return false;
			}

			public JsonArray anomaliesJson()
			{
				return anomalies;
			}
		}

		public void dumpJson()
		{
			StreamWriter stream = null;

			try {
				string json = Json.Serialize(systemJson(), true, "  ");
				string assembly = Assembly.GetExecutingAssembly().Location;
				string directory = Path.GetDirectoryName(assembly);
				string file = Path.Combine(directory, nameof(SolarSystemDump) + ".json");
				if (File.Exists(file))
					return;
				log("dumping to " + file);
				stream = new StreamWriter(file);
				// log("dumping: " + json);
				stream.Write(json);
				stream.Write('\n');
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
			json.Add("g0", PhysicsGlobals.GravitationalAcceleration);

			json.Add("timeUnits", timeUnitsJson());

			JsonObject enums = new JsonObject();
			json.Add("enums", enums);
			addEnumJson(enums, typeof(ExperimentSituations));

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
			info.Add("orbitingBodies", orbitingBodies(body));
			info.Add("albedo", body.albedo);
			info.Add("emissivity", body.emissivity);

			JsonObject size = new JsonObject();
			json.Add("size", size);
			size.Add("radius", body.Radius);
			size.Add("mass", body.Mass);
			size.Add("mu", body.gravParameter);
			size.Add("GeeASL", body.GeeASL);
			size.Add("g0", PhysicsGlobals.GravitationalAcceleration * body.GeeASL);
			size.Add("sphereOfInfluence", body.sphereOfInfluence);
			size.Add("hillSphere", body.hillSphere);
			size.Add("timeWarpAltitudeLimits", toJson(body.timeWarpAltitudeLimits));

			if (body.hasSolidSurface) {
				JsonObject surface = new JsonObject();
				json.Add("surface", surface);
				surface.Add("maxHeight", body.pqsController ? body.pqsController.mapMaxHeight : 0.0);
			}

			if (body.ocean) {
				JsonObject ocean = new JsonObject();
				json.Add("ocean", ocean);
				ocean.Add("density", body.oceanDensity);
				ocean.Add("height", body.pqsController ? body.pqsController.mapOceanHeight : 0.0);
				if (body.ocean && body.pqsController)
					ocean.Add("color", toJson(body.pqsController.mapOceanColor));
			}

			if (body.atmosphere) {
				JsonObject atmosphere = new JsonObject();
				json.Add("atmosphere", atmosphere);
				atmosphere.Add("depth", body.atmosphereDepth);
				atmosphere.Add("containsOxygen", body.atmosphereContainsOxygen);
			}

			{
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
			}

			json.Add("orbit", orbitJson(body));

			{
				JsonObject science = new JsonObject();
				json.Add("science", science);
				if (body.scienceValues != null) {
					CelestialBodyScienceParams sv = body.scienceValues;
					if (body.hasSolidSurface) {
						science.Add("SrfLandedDataValue", sv.LandedDataValue);
					}
					if (body.ocean) {
						science.Add("SrfSplashedDataValue", sv.SplashedDataValue);
					}
					if (body.atmosphere) {
						science.Add("flyingAltitudeThreshold", sv.flyingAltitudeThreshold);
						science.Add("FlyingLowDataValue", sv.FlyingLowDataValue);
						science.Add("FlyingHighDataValue", sv.FlyingHighDataValue);
					}
					science.Add("spaceAltitudeThreshold", sv.spaceAltitudeThreshold);
					science.Add("InSpaceLowDataValue", sv.InSpaceLowDataValue);
					science.Add("InSpaceHighDataValue", sv.InSpaceHighDataValue);
					science.Add("RecoveryDataValue", sv.RecoveryValue);
				}
				science.Add("biomes", toJson(ResearchAndDevelopment.GetBiomeTags(body, false)));
				science.Add("miniBiomes", toJson(ResearchAndDevelopment.GetMiniBiomeTags(body)));

				CBAttributeMapSO bmap = body.BiomeMap;
				if (bmap) {
					JsonObject biomeColors = new JsonObject();
					science.Add("biomeColors", biomeColors);
					for (int i = 0; i < bmap.Attributes.Length; i++) {
						CBAttributeMapSO.MapAttribute a = bmap.Attributes[i];
						biomeColors.Add(a.name.Replace(" ", ""), toJson(a.mapColor));
					}
				}
			}

			{
				AnomalyCollector ac = new AnomalyCollector();

				PQSSurfaceObject[] aa = body.pqsSurfaceObjects;
				if (aa != null)
					for (int i = 0; i < aa.Length; i++)
						ac.visit(aa[i], body);

				List<LaunchSite> ls = PSystemSetup.Instance.LaunchSites;
				if (ls != null) {
					for (int i = 0; i < ls.Count; i++) {
						if (ls[i].Body != body)
							continue;
						ac.visit(ls[i].pqsCity, body);
						ac.visit(ls[i].pqsCity2, body);
					}
				}

				json.Add("anomalies", ac.anomaliesJson());
			}

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

		public static string toJson(Color c)
		{
			StringBuilder ret = new StringBuilder("#");
			for (int i = 0; i < 3; i++) {
				int n = (int) (255f * c[i]);
				if (n < 0)
					n = 0;
				if (n > 255)
					n = 255;
				ret.Append(n.ToString("x2"));
			}
			return ret.ToString();
		}

		public static void addEnumJson(JsonObject json, Type e)
		{
			JsonObject v = new JsonObject();
			json.Add(e.Name, v);
			foreach (int i in Enum.GetValues(e)) {
				v.Add(Enum.GetName(e, i), i);
			}
		}

		public static void log(string msg)
		{
			print(nameof(SolarSystemDump) + ": " + msg);
		}
	}
}
