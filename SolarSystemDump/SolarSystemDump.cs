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
				string infoJson = Json.Serialize(systemInfo(), true, "  ");
				log("dumping info: " + infoJson);
				stream.Write(infoJson);
				stream.Write('\n');
			} catch (Exception e) {
				log("can't save: " + e.Message + "\n" + e.StackTrace);
			} finally {
				dumpedJson = true;
				if (stream != null)
					stream.Close();
			}
		}

		public static object systemInfo()
		{
			Dictionary<string, object> ret = new Dictionary<string, object>();
			ret.Add("version", Versioning.VersionString);
			CelestialBody rootBody = null;
			object bodies = bodiesInfo(ref rootBody);
			if (rootBody)
				ret.Add("rootBody", rootBody.name);
			ret.Add("bodies", bodies);
			return ret;
		}

		public static object bodiesInfo(ref CelestialBody rootBody)
		{
			rootBody = null;
			Dictionary<string, object> ret = new Dictionary<string, object>();
			if (FlightGlobals.Bodies != null) {
				for (int i = 0; i < FlightGlobals.Bodies.Count; i++) {
					CelestialBody body = FlightGlobals.Bodies[i];
					if (body == null || body.name == null)
						continue;
					if (body.orbit == null)
						rootBody = body;
					ret.Add(body.name, bodyInfo(body, i));
				}
			}
			return ret;
		}

		public static Dictionary<string, object> bodyInfo(CelestialBody body, int index)
		{
			Dictionary<string, object> info = new Dictionary<string, object>();
			if (body == null)
				return info;

			info.Add("index", index);
			info.Add("name", body.name);
			info.Add("radius", body.Radius);
			info.Add("mass", body.Mass);
			info.Add("mu", body.gravParameter);
			info.Add("g", G0 * body.GeeASL);
			info.Add("isStar", body.isStar);
			info.Add("isHomeWorld", body.isHomeWorld);
			info.Add("hasSolidSurface", body.hasSolidSurface);
			info.Add("atmosphereDepth", body.atmosphereDepth);
			info.Add("siderealRotationPeriod", body.rotationPeriod);
			info.Add("initialRotationRad", DEG2RAD * body.initialRotation);
			info.Add("initialRotationDeg", body.initialRotation);
			if (body.scienceValues != null)
				info.Add("spaceHighThreshold", body.scienceValues.spaceAltitudeThreshold);
			if (double.IsInfinity(body.sphereOfInfluence)) {
				info.Add("sphereOfInfluence", null);
			} else {
				info.Add("sphereOfInfluence", body.sphereOfInfluence);
			}

			List<object> orbitingBodies = new List<object>();
			info.Add("orbitingBodies", orbitingBodies);
			if (body.orbitingBodies != null && body.orbitingBodies.Count > 0) {
				int childrenCount = body.orbitingBodies.Count;
				log(body.name + " has " + childrenCount + " children");
				for (int i = 0; i < childrenCount; i++) {
					log("child " + i);
					if (body.orbitingBodies[i] != null)
						orbitingBodies.Add(body.orbitingBodies[i].name);
				}
			}

			info.Add("orbit", orbitInfo(body.orbit));

			return info;
		}

		public static Dictionary<string, object> orbitInfo(Orbit orbit)
		{
			if (orbit == null)
				return null;
			Dictionary<string, object> info = new Dictionary<string, object>();
			info.Add("referenceBody", orbit.referenceBody != null ? orbit.referenceBody.name : null);
			info.Add("semiMajorAxis", orbit.semiMajorAxis);
			info.Add("semiLatusRectum", orbit.semiLatusRectum);
			info.Add("eccentricity", orbit.eccentricity);
			info.Add("inclinationRad", DEG2RAD * orbit.inclination);
			info.Add("inclinationDeg", orbit.inclination);
			info.Add("longitudeOfAscendingNodeRad", DEG2RAD * orbit.LAN);
			info.Add("longitudeOfAscendingNodeDeg", orbit.LAN);
			info.Add("argumentOfPeriapsisRad", DEG2RAD * orbit.argumentOfPeriapsis);
			info.Add("argumentOfPeriapsisDeg", orbit.argumentOfPeriapsis);
			info.Add("meanAnomalyAtEpochRad", orbit.meanAnomalyAtEpoch);
			info.Add("meanAnomalyAtEpochDeg", RAD2DEG * orbit.meanAnomalyAtEpoch);
			return info;
		}

		public static void log(string msg)
		{
			print(nameof(SolarSystemDump) + ": " + msg);
		}
	}
}
