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
				string assembly = Assembly.GetExecutingAssembly().Location;
				string directory = Path.GetDirectoryName(assembly);
				string file = Path.Combine(directory, nameof(SolarSystemDump) + ".json");
				log("dumping to " + file);
				stream = new StreamWriter(file);
				string json = Json.Serialize(systemJson(), true, "  ");
				log("dumping: " + json);
				stream.Write(json);
				stream.Write('\n');
			} catch (Exception e) {
				log("can't save: " + e.Message + "\n" + e.StackTrace);
			} finally {
				dumpedJson = true;
				if (stream != null)
					stream.Close();
			}
		}

		public static JsonObject systemJson()
		{
			JsonObject json = new JsonObject();
			json.Add("version", Versioning.VersionString);
			CelestialBody rootBody = null;
			JsonObject bodies = bodiesJson(ref rootBody);
			if (rootBody)
				json.Add("rootBody", rootBody.name);
			json.Add("bodies", bodies);
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

			json.Add("index", index);
			json.Add("name", body.name);
			json.Add("orbitingBodies", orbitingBodies(body));
			json.Add("isStar", body.isStar);
			json.Add("isHomeWorld", body.isHomeWorld);
			json.Add("hasSolidSurface", body.hasSolidSurface);
			json.Add("ocean", body.ocean);
			json.Add("atmosphere", body.atmosphere);
			json.Add("atmosphereContainsOxygen", body.atmosphereContainsOxygen);

			JsonObject size = new JsonObject();
			json.Add("size", size);
			size.Add("radius", body.Radius);
			size.Add("mass", body.Mass);
			size.Add("mu", body.gravParameter);
			size.Add("g0", G0 * body.GeeASL);
			size.Add("atmosphereDepth", body.atmosphereDepth);
			size.Add("spaceHighThreshold",
				body.scienceValues != null ? (object) body.scienceValues.spaceAltitudeThreshold : null);
			size.Add("sphereOfInfluence", body.sphereOfInfluence);
			size.Add("hillSphere", body.hillSphere);

			JsonObject rotation = new JsonObject();
			json.Add("rotation", rotation);
			rotation.Add("axis", vectorJson(body.RotationAxis));
			rotation.Add("solarDayLength", body.solarDayLength);
			rotation.Add("rotationPeriod", body.rotationPeriod);
			rotation.Add("solarRotationPeriod", body.solarRotationPeriod);
			rotation.Add("rotates", body.rotates);
			rotation.Add("tidallyLocked", body.tidallyLocked);
			rotation.Add("initialRotationRad", DEG2RAD * body.initialRotation);
			rotation.Add("initialRotationDeg", body.initialRotation);

			json.Add("orbit", orbitJson(body.orbit));

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

		public static JsonObject orbitJson(Orbit orbit)
		{
			if (orbit == null)
				return null;
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

		public static JsonArray vectorJson(Vector3d v)
		{
			JsonArray ret = new JsonArray();
			ret.Add(v.x);
			ret.Add(v.y);
			ret.Add(v.z);
			return ret;
		}

		public static void log(string msg)
		{
			print(nameof(SolarSystemDump) + ": " + msg);
		}
	}
}
