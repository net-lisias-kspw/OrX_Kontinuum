﻿using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KSP.UI.Screens;

namespace OrX.spawn
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class OrXSpawnHoloKron : MonoBehaviour
    {
        public static OrXSpawnHoloKron instance;
        public Vector3 UpVect;
        public Vector3 EastVect;
        public Vector3 NorthVect;
        public double _lat = 0;
        public double _lon = 0;
        public double _alt = 0;
        public Vessel startGate;
        bool dakarRacing = false;
        public bool openingCraftBrowser = false;
        public CraftBrowserDialog craftBrowser;
        public bool spawningGoal = false;

        private void Awake()
        {
            if (instance) Destroy(instance);
            instance = this;
        }

        public Vector3d WorldPositionToGeoCoords(Vector3d worldPosition, CelestialBody body)
        {
            if (!body)
            {
                return Vector3d.zero;
            }
            double lat = body.GetLatitude(worldPosition);
            double longi = body.GetLongitude(worldPosition);
            double alt = body.GetAltitude(worldPosition);
            Debug.Log("[Spawn OrX HoloKron] Lat: " + lat + " - Lon:" + longi + " - Alt: " + alt);
            return new Vector3d(lat, longi, alt);
        }

        public void StartSpawn(Vector3d stageStartCoords, Vector3d vect, bool Goal, bool empty, bool primary, string HoloKronName, string missionType)
        {
            OrXHoloKron.instance.movingCraft = true;
            OrXHoloKron.instance.getNextCoord = false;
            StartCoroutine(SpawnHoloKron(stageStartCoords, vect, Goal, empty, primary, HoloKronName, missionType));
        }

        IEnumerator SpawnHoloKron(Vector3d stageStartCoords, Vector3d vect, bool b, bool empty, bool primary, string HoloKronName, string missionType)
        {
            string holoFileLoc = UrlDir.ApplicationRootPath + "GameData/OrX/Plugin/PluginData/VesselData/HoloKron/HoloKron.craft";
            _lat = vect.x;
            _lon = vect.y;
            _alt = vect.z;
            bool spawnGate = false;
            yield return new WaitForFixedUpdate();

            if (b)
            {
                Debug.Log("[Spawn OrX HoloKron] Spawning Goal for " + HoloKronName + " " + OrXHoloKron.instance.hkCount);

                if (OrXHoloKron.instance.buildingMission)
                {
                    holoFileLoc = GoalPostCraft;
                }
                else
                {
                    empty = false;
                    b = false;
                    spawnGate = true;
                }
            }
            else
            {
                Debug.Log("[Spawn OrX HoloKron] Spawning " + HoloKronName + " " + OrXHoloKron.instance.hkCount);
            }

            Vector3d tpoint;

            if (empty)
            {
                _alt += 4;
                tpoint = FlightGlobals.ActiveVessel.mainBody.GetWorldSurfacePosition((double)_lat, (double)_lon, (double)_alt)
                    + FlightGlobals.ActiveVessel.transform.forward * 1.5f;
            }

            else
            {
                _alt += 1;

                if (b)
                {
                    _alt += 5;
                }
                tpoint = FlightGlobals.ActiveVessel.mainBody.GetWorldSurfacePosition((double)_lat, (double)_lon, (double)_alt);
            }

            Vector3 gpsPos = WorldPositionToGeoCoords(tpoint, FlightGlobals.currentMainBody);

            Debug.Log("[Spawn OrX HoloKron] Altitude: " + gpsPos.z);

            bool landed = false;
            Orbit orbit = null;

            if (!landed)
            {
                landed = true;

                Vector3d pos = FlightGlobals.currentMainBody.GetRelSurfacePosition(gpsPos.x, gpsPos.y, gpsPos.z);

                orbit = new Orbit(0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, FlightGlobals.currentMainBody);
                orbit.UpdateFromStateVectors(pos, FlightGlobals.currentMainBody.getRFrmVel(pos), FlightGlobals.currentMainBody, Planetarium.GetUniversalTime());
            }

            ConfigNode[] partNodes;
            ShipConstruct shipConstruct = null;
            if (!string.IsNullOrEmpty(holoFileLoc))
            {
                // Save the current ShipConstruction ship, otherwise the player will see the spawned ship next time they enter the VAB!
                ConfigNode currentShip = ShipConstruction.ShipConfig;

                shipConstruct = ShipConstruction.LoadShip(holoFileLoc);
                if (shipConstruct == null)
                {
                    Debug.Log("[Spawn OrX HoloKron] ShipConstruct was null when tried to load '" + holoFileLoc +
                      "' (usually this means the file could not be found).");
                    //return;//continue;
                }

                // Restore ShipConstruction ship
                ShipConstruction.ShipConfig = currentShip;

                // Set the name
                if (string.IsNullOrEmpty(HoloKronName))
                {
                    HoloKronName = "EMPTY";
                }

                // Set some parameters that need to be at the part level
                uint missionID = (uint)Guid.NewGuid().GetHashCode();
                uint launchID = HighLogic.CurrentGame.launchID++;
                foreach (Part p in shipConstruct.parts)
                {
                    p.flightID = ShipConstruction.GetUniqueFlightID(HighLogic.CurrentGame.flightState);
                    p.missionID = missionID;
                    p.launchID = launchID;
                    p.flagURL = "";

                    // Had some issues with this being set to -1 for some ships - can't figure out
                    // why.  End result is the vessel exploding, so let's just set it to a positive
                    // value.
                    p.temperature = 1.0;
                }
                ConfigNode dummyConfig = new ConfigNode();
                ProtoVessel dummyProto = new ProtoVessel(dummyConfig, null);
                Vessel dummyVessel = new Vessel();
                dummyVessel.parts = shipConstruct.parts;
                dummyProto.vesselRef = dummyVessel;

                // Create the ProtoPartSnapshot objects and then initialize them
                foreach (Part p in shipConstruct.parts)
                {
                    dummyProto.protoPartSnapshots.Add(new ProtoPartSnapshot(p, dummyProto));
                }
                foreach (ProtoPartSnapshot p in dummyProto.protoPartSnapshots)
                {
                    p.storePartRefs();
                }

                // Create the ship's parts

                List<ConfigNode> partNodesL = new List<ConfigNode>();
                foreach (ProtoPartSnapshot snapShot in dummyProto.protoPartSnapshots)
                {
                    ConfigNode node = new ConfigNode("PART");
                    snapShot.Save(node);
                    partNodesL.Add(node);
                }
                partNodes = partNodesL.ToArray();
            }
            else
            {
                uint flightId = ShipConstruction.GetUniqueFlightID(HighLogic.CurrentGame.flightState);
                partNodes = new ConfigNode[1];
                partNodes[0] = ProtoVessel.CreatePartNode("EMPTY", flightId, null);

                if (string.IsNullOrEmpty(HoloKronName))
                {
                    HoloKronName = "EMPTY";
                }
            }

            Debug.Log("[Spawn OrX HoloKron] CREATING ADDITIONAL NODES FOR " + HoloKronName + " " + OrXHoloKron.instance.hkCount);

            ConfigNode[] additionalNodes = new ConfigNode[0];
            ConfigNode protoVesselNode = ProtoVessel.CreateVesselNode(HoloKronName, VesselType.Unknown, orbit, 0, partNodes, additionalNodes);

            Vector3d norm = FlightGlobals.currentMainBody.GetRelSurfaceNVector(gpsPos.x, gpsPos.y);

            double terrainHeight = 0.0;
            if (FlightGlobals.currentMainBody.pqsController != null)
            {
                terrainHeight = FlightGlobals.currentMainBody.pqsController.GetSurfaceHeight(norm) - FlightGlobals.currentMainBody.pqsController.radius;
                if (terrainHeight <= FlightGlobals.currentMainBody.pqsController.radius)
                {
                    var tHeight = FlightGlobals.currentMainBody.pqsController.radius - terrainHeight;
                    terrainHeight += tHeight;
                }
            }
            bool splashed = false;

            protoVesselNode.SetValue("sit", (splashed ? Vessel.Situations.SPLASHED : landed ?
              Vessel.Situations.LANDED : Vessel.Situations.FLYING).ToString());
            protoVesselNode.SetValue("landed", (landed && !splashed).ToString());
            protoVesselNode.SetValue("splashed", splashed.ToString());
            protoVesselNode.SetValue("lat", gpsPos.x.ToString());
            protoVesselNode.SetValue("lon", gpsPos.y.ToString());
            protoVesselNode.SetValue("alt", gpsPos.z.ToString());
            protoVesselNode.SetValue("landedAt", FlightGlobals.currentMainBody.name);

            Debug.Log("[Spawn OrX HoloKron] Figure out the surface height and rotation for " + HoloKronName + " " + OrXHoloKron.instance.hkCount);

            Quaternion normal = Quaternion.LookRotation((Vector3)norm);
            Quaternion rotation = Quaternion.identity;
            rotation = rotation * Quaternion.FromToRotation(Vector3.up, Vector3.forward);
            rotation = Quaternion.FromToRotation(Vector3.up, -Vector3.up) * rotation;

            protoVesselNode.SetValue("hgt", "0", true);
            protoVesselNode.SetValue("rot", KSPUtil.WriteQuaternion(normal * rotation), true);
            Vector3 nrm = (rotation * Vector3.forward);
            protoVesselNode.SetValue("nrm", nrm.x + "," + nrm.y + "," + nrm.z, true);
            protoVesselNode.SetValue("prst", false.ToString(), true);

            ProtoVessel protoVessel = HighLogic.CurrentGame.AddVessel(protoVesselNode);
            Vessel holoCube = protoVessel.vesselRef;
            foreach (Part p in FindObjectsOfType<Part>())
            {
                if (!p.vessel)
                {
                    Destroy(p.gameObject);
                }
            }
            yield return new WaitForFixedUpdate();
            holoCube.IgnoreGForces(240);
            holoCube.isPersistent = true;
            holoCube.Landed = false;
            holoCube.situation = Vessel.Situations.FLYING;
            while (holoCube.packed)
            {
                yield return null;
            }
            holoCube.SetWorldVelocity(Vector3d.zero);
            yield return null;
            holoCube.IgnoreGForces(240);

            var mom = holoCube.rootPart.FindModuleImplementing<ModuleOrXMission>();
            if (mom == null)
            {
                holoCube.rootPart.AddModule("ModuleOrXMission", true);
                mom = holoCube.rootPart.FindModuleImplementing<ModuleOrXMission>();
            }
            mom.HoloKronName = HoloKronName;
            mom.missionType = missionType;
            mom.latitude = tpoint.x;
            mom.longitude = tpoint.y;
            mom.altitude = tpoint.z;
            mom.pos = tpoint;
            holoCube.GoOffRails();
            holoCube.IgnoreGForces(240);
            StageManager.BeginFlight();

            if (!b)
            {
                holoCube.IgnoreGForces(240);

                if (!empty)
                {
                    StartCoroutine(SpawnLocalVessels(primary, HoloKronName, tpoint));
                    OrXHoloKron.instance.OrXHCGUIEnabled = false;
                    if (spawnGate)
                    {
                        mom.Goal = true;
                        OrXHoloKron.instance.targetCoord = holoCube;
                    }
                }
                else
                {
                    OrXHoloKron.instance.SetupHolo(holoCube, new Vector3d(_lat, _lon, _alt));
                }
            }
            else
            {
                mom.Goal = true;
                OrXHoloKron.instance.targetCoord = holoCube;

                if (OrXHoloKron.instance.buildingMission)
                {
                    Debug.Log("[OrX Spawn Vessels Locally] Beginning flight ..................");
                    
                    Quaternion _fixRot = Quaternion.identity;
                    holoCube.IgnoreGForces(240);
                    holoCube.angularVelocity = Vector3.zero;
                    holoCube.angularMomentum = Vector3.zero;
                    holoCube.SetWorldVelocity(Vector3d.zero);

                    Vector3 _startPos = FlightGlobals.ActiveVessel.mainBody.GetWorldSurfacePosition((double)stageStartCoords.x, (double)stageStartCoords.y, (double)stageStartCoords.z);
                    Vector3 _goalPos = FlightGlobals.ActiveVessel.mainBody.GetWorldSurfacePosition((double)vect.x, (double)vect.y, (double)vect.z);
                    Vector3 startPosDirection = (_goalPos - _startPos).normalized;

                    _fixRot = Quaternion.FromToRotation(holoCube.ReferenceTransform.right, startPosDirection) * holoCube.ReferenceTransform.rotation;
                    holoCube.SetRotation(_fixRot, true);
                    _fixRot = Quaternion.AngleAxis(-90, holoCube.ReferenceTransform.right) * holoCube.ReferenceTransform.rotation;
                    holoCube.SetRotation(_fixRot, true);
                   
                    holoCube.vesselName = HoloKronName + " STAGE " + (OrXHoloKron.instance.hkCount + 1) + " FINISH LINE";
                    OrXHoloKron.instance.movingCraft = true;
                    OrXHoloKron.instance.getNextCoord = true;
                    OrXHoloKron.instance.spawningStartGate = true;
                    OrXVesselMove.Instance.StartMove(holoCube, false, 0, true);
                }
            }
            mom.isLoaded = true;
        }
        IEnumerator SpawnLocalVessels(bool primary, string HoloKronName, Vector3d vect)
        {
            string missionCraftLoc = UrlDir.ApplicationRootPath + "GameData/OrX/Plugin/PluginData/spawn.tmp";
            string pas = "";
            float _altToSubtract = 0;
            double mPerDegree = (((2 * (FlightGlobals.ActiveVessel.mainBody.Radius + FlightGlobals.ActiveVessel.altitude)) * Math.PI) / 360);
            double degPerMeter = 1 / mPerDegree;
            double targetDistance = double.MaxValue;
            double _latDiff = 0;
            double _lonDiff = 0;
            double _altDiff = 0;
            UpVect = (FlightGlobals.ActiveVessel.ReferenceTransform.position - FlightGlobals.ActiveVessel.mainBody.position).normalized;
            EastVect = FlightGlobals.ActiveVessel.mainBody.getRFrmVel(FlightGlobals.ActiveVessel.CoM).normalized;
            NorthVect = Vector3.Cross(EastVect, UpVect).normalized;

            Debug.Log("[OrX Spawn Local Vessels] === Spawning Local Vessels === ");
            ConfigNode _file = new ConfigNode();
            _file = ConfigNode.Load(UrlDir.ApplicationRootPath + "GameData/OrX/HoloKron/" + HoloKronName + ".orx");

            ConfigNode _toArchive = ConfigNode.Load(UrlDir.ApplicationRootPath + "GameData/OrX/HoloArchive/" + HoloKronName + ".orx");
            if (_toArchive == null)
            {
                _toArchive = new ConfigNode();
                _toArchive = _file;
                _toArchive.Save(UrlDir.ApplicationRootPath + "GameData/OrX/HoloArchive/" + HoloKronName + ".orx");
            }

            if (HoloKronName != "")
            {
                int _vesselCount = 1;
                int _hkCount = 0;
                ConfigNode node = _file.GetNode("OrX");

                foreach (ConfigNode spawnCheck in node.nodes)
                {
                    if (spawnCheck.name.Contains("OrXHoloKronCoords" + _hkCount))
                    {
                        Debug.Log("[OrX Spawn Local Vessels] === FOUND " + HoloKronName + " " + _hkCount + " ... DECRYPTING ===");

                        foreach (ConfigNode.Value data in spawnCheck.values)
                        {
                            if (data.name == "spawned")
                            {
                                if (data.value == "False")
                                {
                                    Debug.Log("[OrX Spawn Local Vessels] ===  " + HoloKronName + " " + _hkCount + " has not spawned ===");
                                    spawnCheck.SetValue("spawned", "True", true);
                                    _file.Save(UrlDir.ApplicationRootPath + "GameData/OrX/HoloKron/" + HoloKronName + ".orx");
                                    break;
                                }
                                else
                                {
                                    Debug.Log("[OrX Spawn Local Vessels] === " + HoloKronName + " " + _hkCount + " has spawned ... CHECKING FOR EXTRAS");

                                    if (spawnCheck.HasValue("extras"))
                                    {
                                        if (spawnCheck.GetValue("extras") == "False")
                                        {
                                            Debug.Log("[OrX Spawn Local Vessels] === " + HoloKronName + " " + _hkCount + " has no extras ... END TRANSMISSION");
                                            _hkCount = 1138;
                                            primary = true;
                                            break;
                                        }
                                        else
                                        {
                                            Debug.Log("[OrX Spawn Local Vessels] === " + HoloKronName + " " + _hkCount + " has extras ... SEARCHING");
                                            _hkCount += 1;
                                        }
                                    }
                                }
                            }
                        }

                        Debug.Log("[OrX Spawn Local Vessels] === DATA PROCESSED ===");
                    }
                }


                foreach (ConfigNode _vts in node.nodes)
                {
                    if (_vts.name.Contains("HC" + _hkCount + "OrXv" + _vesselCount))
                    {
                        Debug.Log("[OrX Spawn Local Vessels] === GRABBING CRAFT FILE FOR " + _vts.name + " ===");
                        _vesselCount += 1;
                        float _left = 0;
                        float _pitch = 0;
                        double _al = 0;
                        double _la = 0;
                        double _lo = 0;
                        int _serial = 1138;

                        ConfigNode location = _vts.GetNode("coords");

                        foreach (ConfigNode.Value loc in location.values)
                        {
                            string cvEncryptedName = OrXLog.instance.Decrypt(loc.name);
                            string cvEncryptedValue = OrXLog.instance.Decrypt(loc.value);
                            loc.name = cvEncryptedName;
                            loc.value = cvEncryptedValue;

                            if (loc.name == "lat")
                            {
                                _la = double.Parse(loc.value);
                            }
                            if (loc.name == "lon")
                            {
                                _lo = double.Parse(loc.value);
                            }
                            if (loc.name == "alt")
                            {
                                _al = double.Parse(loc.value);
                            }
                            if (loc.name == "left")
                            {
                                _left = float.Parse(loc.value);
                            }
                            if (loc.name == "pitch")
                            {
                                _pitch = float.Parse(loc.value);
                            }
                            if (loc.name == "pas")
                            {
                                pas = loc.value;
                            }
                        }

                        Debug.Log("[OrX Spawn Local Vessels] === VESSEL SPAWN COORDS READY ===");

                        Debug.Log("[OrX Spawn Local Vessels] === DECRYPTING CRAFT FILE DATA FOR " + _vts.name + " ===");
                        string _vesselName = string.Empty;
                        ConfigNode craftFile = _vts.GetNode("craft");
                        foreach (ConfigNode.Value cv in craftFile.values)
                        {
                            string cvEncryptedName = OrXLog.instance.Decrypt(cv.name);
                            string cvEncryptedValue = OrXLog.instance.Decrypt(cv.value);
                            cv.name = cvEncryptedName;
                            cv.value = cvEncryptedValue;

                            if (cv.name == "name")
                            {
                                _vesselName = cv.value;
                            }

                            if (cv.name == "size")
                            {
                                string[] _sizeString = cv.value.Split(new char[] { ',' });

                                try
                                {
                                    if (_sizeString[0] != null && _sizeString[0].Length > 0 && _sizeString[0] != "null")
                                    {
                                        for (int i = 0; i < _sizeString.Length; i++)
                                        {
                                            if (_sizeString[i] != null && _sizeString[i].Length > 0)
                                            {
                                                float _maxValue = Math.Max(float.Parse(_sizeString[0]), Math.Max(float.Parse(_sizeString[1]), float.Parse(_sizeString[2])));
                                                _altToSubtract = _maxValue / 2;
                                            }
                                        }
                                    }
                                }
                                catch (IndexOutOfRangeException e)
                                {
                                    Debug.Log("[OrX Spawn Local Vessels] Altitude calculated ...... ");
                                }
                            }
                        }


                        foreach (ConfigNode cn in craftFile.nodes)
                        {
                            foreach (ConfigNode.Value cv in cn.values)
                            {
                                string cvEncryptedName = OrXLog.instance.Decrypt(cv.name);
                                string cvEncryptedValue = OrXLog.instance.Decrypt(cv.value);
                                cv.name = cvEncryptedName;
                                cv.value = cvEncryptedValue;
                            }

                            foreach (ConfigNode cn2 in cn.nodes)
                            {
                                foreach (ConfigNode.Value cv2 in cn2.values)
                                {
                                    if (cv2.name != "currentRotation")
                                    {
                                        string cvEncryptedName = OrXLog.instance.Decrypt(cv2.name);
                                        string cvEncryptedValue = OrXLog.instance.Decrypt(cv2.value);
                                        cv2.name = cvEncryptedName;
                                        cv2.value = cvEncryptedValue;
                                    }
                                }
                            }
                        }

                        Debug.Log("[OrX Spawn Local Vessels] === VESSEL DECRYPTED - CHECKING MODULES ===");
                        List<string> partsLoaded = new List<string>();
                        int partCount = 0;



                        foreach (ConfigNode partCheck in craftFile.nodes)
                        {
                            if (node.name == "PART")
                            {
                                foreach (ConfigNode.Value partCheck2 in partCheck.values)
                                {
                                    if (partCheck2.name == "name")
                                    {
                                        if (!partsLoaded.Contains(partCheck2.value))
                                        {
                                            partCount += 1;
                                            partsLoaded.Add(partCheck2.value);
                                        }
                                    }
                                }
                            }
                        }


                        List<AvailablePart>.Enumerator availablePart = PartLoader.LoadedPartsList.GetEnumerator();
                        while (availablePart.MoveNext())
                        {
                            try
                            {
                                if (availablePart.Current != null)
                                {
                                    if (partsLoaded.Contains(availablePart.Current.name))
                                    {
                                        partCount -= 1;
                                        partsLoaded.Remove(availablePart.Current.name);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.Log("[OrX Spawn Local Vessels] === " + e + " ===");
                            }
                        }
                        availablePart.Dispose();

                        yield return new WaitForFixedUpdate();

                        bool okToLoad = true;
                        if (partsLoaded.Count >= 0)
                        {
                            foreach (string s in partsLoaded)
                            {
                                okToLoad = false;
                                Debug.Log("[OrX Spawn Local Vessels] === " + _vts.name + " CONTAINS UNRECOGNIZED PARTS ... SKIPPING ===");
                                Debug.Log("[OrX Spawn Local Vessels] === " + s + " UNRECOGNIZED ===");
                            }
                        }

                        List<Vessel>.Enumerator _v = FlightGlobals.VesselsLoaded.GetEnumerator();
                        while (_v.MoveNext())
                        {
                            if (_v.Current == null) continue;
                            if (_v.Current.vesselName == _vesselName)
                            {
                                if (_al <= _v.Current.altitude)
                                {
                                    _altDiff = _v.Current.altitude - _al;
                                }
                                else
                                {
                                    _altDiff = _al - _v.Current.altitude;
                                }

                                if (_v.Current.latitude >= 0)
                                {
                                    if (_la >= _v.Current.latitude)
                                    {
                                        _latDiff = _la - _v.Current.latitude;
                                    }
                                    else
                                    {
                                        _latDiff = _v.Current.latitude - _la;
                                    }
                                }
                                else
                                {
                                    if (_la >= 0)
                                    {
                                        _latDiff = _la - _v.Current.latitude;
                                    }
                                    else
                                    {
                                        if (_la <= _v.Current.latitude)
                                        {
                                            _latDiff = _la - _v.Current.latitude;
                                        }
                                        else
                                        {

                                            _latDiff = _v.Current.latitude - _la;
                                        }
                                    }
                                }

                                if (_v.Current.longitude >= 0)
                                {
                                    if (_lo >= _v.Current.longitude)
                                    {
                                        _lonDiff = _lo - _v.Current.longitude;
                                    }
                                    else
                                    {
                                        _lonDiff = _v.Current.longitude - _lo;
                                    }
                                }
                                else
                                {
                                    if (_lo >= 0)
                                    {
                                        _lonDiff = _lo - _v.Current.longitude;
                                    }
                                    else
                                    {
                                        if (_lo <= _v.Current.longitude)
                                        {
                                            _lonDiff = _lo - _v.Current.longitude;
                                        }
                                        else
                                        {

                                            _lonDiff = _v.Current.longitude - _lo;
                                        }
                                    }
                                }

                                double diffSqr = (_latDiff * _latDiff) + (_lonDiff * _lonDiff);
                                double _altDiffDeg = _altDiff * degPerMeter;
                                double altAdded = (_altDiffDeg * _altDiffDeg) + diffSqr;
                                double _targetDistance = Math.Sqrt(altAdded) * mPerDegree;

                                if (_targetDistance <= 5)
                                {
                                    okToLoad = false;
                                    Debug.Log("[OrX Spawn Local Vessels] === " + _vts.name + " already present ===");

                                }
                            }
                        }
                        _v.Dispose();

                        if (okToLoad)
                        {
                            Debug.Log("[OrX Spawn Local Vessels] === " + _vesselName + " READY FOR SPAWNING ===");

                            craftFile.Save(UrlDir.ApplicationRootPath + "GameData/OrX/Plugin/PluginData/spawn.tmp");

                            Vector3d tpoint = FlightGlobals.ActiveVessel.mainBody.GetWorldSurfacePosition((double)_la, (double)_lo, (double)_al + (_altToSubtract * 3));
                            Vector3 gpsPos = WorldPositionToGeoCoords(tpoint, FlightGlobals.currentMainBody);
                            Orbit orbit = null;
                            //HoloKronName = _vesselName;

                            Debug.Log("[OrX Spawn Local Vessels] Altitude: " + gpsPos.z);

                            bool landed = false;
                            if (!landed)
                            {
                                landed = true;

                                Vector3d pos = FlightGlobals.currentMainBody.GetRelSurfacePosition(gpsPos.x, gpsPos.y, gpsPos.z);
                                orbit = new Orbit(0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, FlightGlobals.currentMainBody);
                                orbit.UpdateFromStateVectors(pos, FlightGlobals.currentMainBody.getRFrmVel(pos), FlightGlobals.currentMainBody, Planetarium.GetUniversalTime());
                            }

                            Debug.Log("[OrX Spawn Local Vessels] Orbit Data Processed");

                            ConfigNode[] partNodes;
                            ShipConstruct shipConstruct = null;

                            ConfigNode currentShip = ShipConstruction.ShipConfig;
                            shipConstruct = ShipConstruction.LoadShip("GameData/OrX/Plugin/PluginData/spawn.tmp");
                            ShipConstruction.ShipConfig = currentShip;
                            uint missionID = (uint)Guid.NewGuid().GetHashCode();
                            uint launchID = HighLogic.CurrentGame.launchID++;

                            Debug.Log("[OrX Spawn Local Vessels] Ship construct created");
                            Debug.Log("[OrX Spawn Local Vessels] Checking for crew space");

                            foreach (Part p in shipConstruct.parts)
                            {
                                p.flightID = ShipConstruction.GetUniqueFlightID(HighLogic.CurrentGame.flightState);
                                p.missionID = missionID;
                                p.launchID = launchID;
                                p.flagURL = "";
                                p.temperature = 1.0;
                            }

                            Debug.Log("[OrX Spawn Local Vessels] Part flight ID's processed");
                            Debug.Log("[OrX Spawn Local Vessels] Constructing protovessel");

                            ConfigNode empty = new ConfigNode();
                            ProtoVessel dummyProto = new ProtoVessel(empty, null);
                            Vessel dummyVessel = new Vessel();
                            dummyVessel.parts = shipConstruct.parts;
                            dummyProto.vesselRef = dummyVessel;

                            foreach (Part p in shipConstruct.parts)
                            {
                                dummyProto.protoPartSnapshots.Add(new ProtoPartSnapshot(p, dummyProto));
                            }

                            if (OrXHoloKron.instance.bdaChallenge)
                            {
                                Part part = shipConstruct.parts.Find(p => p.protoModuleCrew.Count < p.CrewCapacity);

                                if (part != null)
                                {
                                    ProtoCrewMember crewMember = HighLogic.CurrentGame.CrewRoster.GetNewKerbal();
                                    crewMember.gender = UnityEngine.Random.Range(0, 100) > 50
                                      ? ProtoCrewMember.Gender.Female
                                      : ProtoCrewMember.Gender.Male;
                                    System.Random r = new System.Random();
                                    crewMember.trait = "Pilot";
                                    part.AddCrewmemberAt(crewMember, part.protoModuleCrew.Count);
                                }
                            }

                            foreach (ProtoPartSnapshot p in dummyProto.protoPartSnapshots)
                            {
                                p.storePartRefs();
                            }

                            List<ConfigNode> partNodesL = new List<ConfigNode>();
                            foreach (ProtoPartSnapshot snapShot in dummyProto.protoPartSnapshots)
                            {
                                ConfigNode partNode = new ConfigNode("PART");
                                snapShot.Save(partNode);
                                partNodesL.Add(partNode);
                            }
                            partNodes = partNodesL.ToArray();

                            Debug.Log("[OrX Spawn Local Vessels] CREATING ADDITIONAL NODES FOR " + HoloKronName);
                            ConfigNode[] additionalNodes = new ConfigNode[0];
                            ConfigNode protoVesselNode = ProtoVessel.CreateVesselNode(HoloKronName, VesselType.Unknown, orbit, 0, partNodes, additionalNodes);
                            bool splashed = false;

                            Vector3d norm = FlightGlobals.currentMainBody.GetRelSurfaceNVector(gpsPos.x, gpsPos.y);
                            splashed = false;
                            protoVesselNode.SetValue("sit", (splashed ? Vessel.Situations.SPLASHED : landed ?
                              Vessel.Situations.LANDED : Vessel.Situations.FLYING).ToString());
                            protoVesselNode.SetValue("landed", (landed && !splashed).ToString());
                            protoVesselNode.SetValue("splashed", splashed.ToString());
                            protoVesselNode.SetValue("lat", gpsPos.x.ToString());
                            protoVesselNode.SetValue("lon", gpsPos.y.ToString());
                            protoVesselNode.SetValue("alt", gpsPos.z.ToString());
                            protoVesselNode.SetValue("landedAt", FlightGlobals.currentMainBody.name);

                            Quaternion normal = Quaternion.LookRotation((Vector3)norm);
                            Quaternion rotation = Quaternion.identity;
                            rotation = rotation * Quaternion.FromToRotation(Vector3.up, Vector3.forward);
                            rotation = Quaternion.FromToRotation(Vector3.up, -Vector3.up) * rotation;



                            protoVesselNode.SetValue("hgt", "0", true);
                            protoVesselNode.SetValue("rot", KSPUtil.WriteQuaternion(normal * rotation), true);
                            Vector3 nrm = (rotation * Vector3.forward);
                            protoVesselNode.SetValue("nrm", nrm.x + "," + nrm.y + "," + nrm.z, true);
                            protoVesselNode.SetValue("prst", false.ToString(), true);

                            ProtoVessel protoVessel = HighLogic.CurrentGame.AddVessel(protoVesselNode);
                            protoVessel.vesselRef.transform.rotation = protoVessel.rotation;
                            Vessel localVessel = protoVessel.vesselRef;

                            foreach (Part p in FindObjectsOfType<Part>())
                            {
                                if (!p.vessel)
                                {
                                    Destroy(p.gameObject);
                                }
                            }

                            yield return new WaitForFixedUpdate();
                            ConfigNode craft = ConfigNode.Load(missionCraftLoc);
                            craft.ClearData();
                            craft.Save(missionCraftLoc);

                            localVessel.isPersistent = true;
                            localVessel.Landed = false;
                            localVessel.situation = Vessel.Situations.FLYING;
                            while (localVessel.packed)
                            {
                                yield return null;
                            }
                            localVessel.SetWorldVelocity(Vector3d.zero);
                            localVessel.GoOffRails();
                            localVessel.IgnoreGForces(240);
                            localVessel.rootPart.AddModule("ModuleHideVessel", true);
                            localVessel.rootPart.AddModule("ModuleParkingBrake", true);
                            StageManager.BeginFlight();

                            Debug.Log("[OrX Spawn Local Vessels] === FIXING ROTATION FOR " + _vts.name + " ===");
                            if (_left >= 360)
                            {
                                _left -= 360;
                            }

                            UpVect = (localVessel.transform.position - localVessel.mainBody.position).normalized;
                            EastVect = localVessel.mainBody.getRFrmVel(localVessel.CoM).normalized;
                            NorthVect = Vector3.Cross(EastVect, UpVect).normalized;
                            Quaternion _fixRot = Quaternion.identity;

                            localVessel.IgnoreGForces(240);
                            localVessel.angularVelocity = Vector3.zero;
                            localVessel.angularMomentum = Vector3.zero;
                            localVessel.SetWorldVelocity(Vector3d.zero);
                            _fixRot = Quaternion.FromToRotation(-localVessel.ReferenceTransform.right, NorthVect) * localVessel.ReferenceTransform.rotation;
                            localVessel.SetRotation(_fixRot, true);
                            _fixRot = Quaternion.AngleAxis(180, localVessel.ReferenceTransform.right) * localVessel.ReferenceTransform.rotation;
                            localVessel.SetRotation(_fixRot, true);
                            localVessel.IgnoreGForces(240);
                            _fixRot = Quaternion.AngleAxis(_pitch - 90, localVessel.ReferenceTransform.right) * localVessel.ReferenceTransform.rotation;
                            localVessel.SetRotation(_fixRot, true);
                            _fixRot = Quaternion.AngleAxis(_left, UpVect) * localVessel.ReferenceTransform.rotation;
                            localVessel.SetRotation(_fixRot, true);
                            float localAlt = Convert.ToSingle(localVessel.radarAltitude);
                            Debug.Log("[OrX Spawn Local Vessels] === PLACING " + _vts.name + " ===");
                            localAlt -= _altToSubtract;
                            float dropRate = Mathf.Clamp((localAlt * 2), 0.1f, 200);

                            while (!localVessel.LandedOrSplashed)
                            {
                                localVessel.IgnoreGForces(240);
                                localVessel.angularVelocity = Vector3.zero;
                                localVessel.angularMomentum = Vector3.zero;
                                localVessel.SetWorldVelocity(Vector3.zero);
                                dropRate = Mathf.Clamp((localAlt * 2), 0.1f, 200);

                                if (dropRate > 3)
                                {
                                    localVessel.Translate(dropRate * Time.fixedDeltaTime * -UpVect);
                                }
                                else
                                {
                                    localVessel.SetWorldVelocity(dropRate * -UpVect);
                                }

                                if (localAlt <= 1f)
                                {
                                    localAlt = 1f;
                                }

                                localAlt -= dropRate * Time.fixedDeltaTime;

                                yield return new WaitForFixedUpdate();
                            }
                            Debug.Log("[OrX Spawn Local Vessels] === " + _vts.name + " PLACED ===");

                        }
                    }
                }


                OrXHoloKron.instance.movingCraft = false;
                OrXHoloKron.instance.GuiEnabledOrXMissions = false;
                OrXHoloKron.instance.OrXHCGUIEnabled = false;
                OrXHoloKron.instance.checking = false;
            }
        }

        Vector3d _pos;
        Vector3 CrossVect;
        bool post1spawned = false;
        string GoalPostCraft = UrlDir.ApplicationRootPath + "GameData/OrX/Plugin/PluginData/VesselData/Goal/Goal.craft";

        public void SpawnGatePosts(bool post1, Vector3d goalCoords, Vector3d stageStartCoords)
        {
            Vector3 _startPos = FlightGlobals.ActiveVessel.mainBody.GetWorldSurfacePosition((double)stageStartCoords.x, (double)stageStartCoords.y, (double)stageStartCoords.z);
            Vector3 _goalPos = FlightGlobals.ActiveVessel.mainBody.GetWorldSurfacePosition((double)goalCoords.x, (double)goalCoords.y, (double)goalCoords.z);
            Vector3 startPosDirection = (_goalPos - _startPos).normalized;
            UpVect = (FlightGlobals.ActiveVessel.transform.position - FlightGlobals.ActiveVessel.mainBody.position).normalized;
            CrossVect = Vector3.Cross(startPosDirection, UpVect).normalized;

            StartCoroutine(SpawnVesselLocally(GoalPostCraft, goalCoords, true, stageStartCoords));
        }
        IEnumerator SpawnVesselLocally(string craftFile, Vector3d goalCoords, bool isGoal, Vector3d stageStartCoords)
        {
            float _altToSubtract = 0;
            Orbit _orbit = null;
            string hn = "";

            if (isGoal)
            {
                if (!post1spawned)
                {
                    post1spawned = true;
                    goalCoords = goalCoords + CrossVect * 5f;
                    Debug.Log("[OrX Spawn Vessels Locally] Goal Post 1 Spawning ............. ");
                }
                else
                {
                    post1spawned = false;
                    goalCoords = goalCoords - CrossVect * 5f;
                    Debug.Log("[OrX Spawn Vessels Locally] Goal Post 2 Spawning ............. ");
                }
            }

            Vector3d tpoint = FlightGlobals.ActiveVessel.mainBody.GetWorldSurfacePosition((double)goalCoords.x, (double)goalCoords.y, (double)goalCoords.z + 20);
            Vector3 gpsPos = WorldPositionToGeoCoords(tpoint, FlightGlobals.currentMainBody);

            ConfigNode _craftFile = ConfigNode.Load(craftFile);
            foreach (ConfigNode.Value cv in _craftFile.values)
            {
                if (cv.name == "ship")
                {
                    hn = cv.value;
                }

                if (cv.name == "size")
                {
                    string[] _sizeString = cv.value.Split(new char[] { ',' });

                    try
                    {
                        if (_sizeString[0] != null && _sizeString[0].Length > 0 && _sizeString[0] != "null")
                        {
                            for (int i = 0; i < _sizeString.Length; i++)
                            {
                                if (_sizeString[i] != null && _sizeString[i].Length > 0)
                                {
                                    float _maxValue = Math.Max(float.Parse(_sizeString[0]), Math.Max(float.Parse(_sizeString[0]), float.Parse(_sizeString[0])));
                                    _altToSubtract = _maxValue / 2;
                                }
                            }
                        }
                    }
                    catch (IndexOutOfRangeException e)
                    {
                        Debug.Log("[OrX Spawn Local Vessels] Altitude calculated ...... ");
                    }
                }
            }

            Debug.Log("[OrX Spawn Vessels Locally] Spawning " + hn);

            bool landed = false;
            if (!landed)
            {
                landed = true;

                yield return new WaitForFixedUpdate();
                _pos = FlightGlobals.currentMainBody.GetRelSurfacePosition(gpsPos.x, gpsPos.y, gpsPos.z + (_altToSubtract * 3));
                _orbit = new Orbit(0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, FlightGlobals.currentMainBody);
                _orbit.UpdateFromStateVectors(_pos, FlightGlobals.currentMainBody.getRFrmVel(_pos), FlightGlobals.currentMainBody, Planetarium.GetUniversalTime());
            }

            Debug.Log("[OrX Spawn Vessels Locally] Orbit Data Processed");

            ConfigNode[] partNodes;
            ShipConstruct shipConstruct = null;

            ConfigNode currentShip = ShipConstruction.ShipConfig;
            shipConstruct = ShipConstruction.LoadShip(craftFile);
            ShipConstruction.ShipConfig = currentShip;
            uint missionID = (uint)Guid.NewGuid().GetHashCode();
            uint launchID = HighLogic.CurrentGame.launchID++;

            Debug.Log("[OrX Spawn Vessels Locally] Ship construct created");

            foreach (Part p in shipConstruct.parts)
            {
                p.flightID = ShipConstruction.GetUniqueFlightID(HighLogic.CurrentGame.flightState);
                p.missionID = missionID;
                p.launchID = launchID;
                p.flagURL = "";
                p.temperature = 1.0;
            }

            Debug.Log("[OrX Spawn Vessels Locally] Part flight ID's processed");
            Debug.Log("[OrX Spawn Vessels Locally] Constructing protovessel");

            ConfigNode empty = new ConfigNode();
            ProtoVessel dummyProto = new ProtoVessel(empty, null);
            Vessel dummyVessel = new Vessel();
            dummyVessel.parts = shipConstruct.parts;
            dummyProto.vesselRef = dummyVessel;

            foreach (Part p in shipConstruct.parts)
            {
                dummyProto.protoPartSnapshots.Add(new ProtoPartSnapshot(p, dummyProto));
            }

            foreach (ProtoPartSnapshot p in dummyProto.protoPartSnapshots)
            {
                p.storePartRefs();
            }

            List<ConfigNode> partNodesL = new List<ConfigNode>();
            foreach (ProtoPartSnapshot snapShot in dummyProto.protoPartSnapshots)
            {
                ConfigNode partNode = new ConfigNode("PART");
                snapShot.Save(partNode);
                partNodesL.Add(partNode);
            }
            partNodes = partNodesL.ToArray();

            Debug.Log("[OrX Spawn Vessels Locally] CREATING ADDITIONAL NODES FOR " + hn);
            ConfigNode[] additionalNodes = new ConfigNode[0];
            ConfigNode protoVesselNode = ProtoVessel.CreateVesselNode(hn, VesselType.Unknown, _orbit, 0, partNodes, additionalNodes);
            bool splashed = false;
            Debug.Log("[OrX Spawn Vessels Locally] protoVesselNode CREATED FOR " + hn);

            Vector3d norm = FlightGlobals.currentMainBody.GetRelSurfaceNVector(gpsPos.x, gpsPos.y);
            splashed = false;
            protoVesselNode.SetValue("sit", (splashed ? Vessel.Situations.SPLASHED : landed ?
              Vessel.Situations.LANDED : Vessel.Situations.FLYING).ToString());
            protoVesselNode.SetValue("landed", (landed && !splashed).ToString());
            protoVesselNode.SetValue("splashed", splashed.ToString());
            protoVesselNode.SetValue("lat", gpsPos.x.ToString());
            protoVesselNode.SetValue("lon", gpsPos.y.ToString());
            protoVesselNode.SetValue("alt", gpsPos.z.ToString());
            protoVesselNode.SetValue("landedAt", FlightGlobals.currentMainBody.name);
            Debug.Log("[OrX Spawn Vessels Locally] protoVesselNode VALUES SET FOR " + hn);

            Quaternion normal = Quaternion.LookRotation((Vector3)norm);
            Quaternion rotation = Quaternion.identity;
            float heading = 90;
            int pitch = 0;
            if (shipConstruct == null)
            {
                rotation = rotation * Quaternion.FromToRotation(Vector3.up, Vector3.back);
            }
            else if (shipConstruct.shipFacility == EditorFacility.SPH)
            {
                rotation = rotation * Quaternion.FromToRotation(Vector3.forward, -Vector3.forward);
                heading += 180.0f;
            }
            else
            {
                rotation = rotation * Quaternion.FromToRotation(Vector3.up, Vector3.forward);
                rotation = Quaternion.FromToRotation(Vector3.up, -Vector3.up) * rotation;
                pitch = 0;
            }
            rotation = rotation * Quaternion.AngleAxis(heading, Vector3.back);
            rotation = rotation * Quaternion.AngleAxis(0, Vector3.down);
            rotation = rotation * Quaternion.AngleAxis(pitch, Vector3.left);

            Debug.Log("[OrX Spawn Vessels Locally] protoVesselNode rotation CALCULATED FOR " + hn);

            protoVesselNode.SetValue("hgt", "0", true);
            protoVesselNode.SetValue("rot", KSPUtil.WriteQuaternion(normal * rotation), true);
            Vector3 nrm = (rotation * Vector3.forward);
            protoVesselNode.SetValue("nrm", nrm.x + "," + nrm.y + "," + nrm.z, true);
            protoVesselNode.SetValue("prst", false.ToString(), true);

            Debug.Log("[OrX Spawn Vessels Locally] protoVesselNode rotation SAVED FOR " + hn);

            ProtoVessel protoVessel = HighLogic.CurrentGame.AddVessel(protoVesselNode);
            protoVessel.vesselRef.transform.rotation = protoVessel.rotation;
            Vessel localVessel = protoVessel.vesselRef;

            Debug.Log("[OrX Spawn Vessels Locally] Applying parking brake ..................");

            foreach (Part p in FindObjectsOfType<Part>())
            {
                if (!p.vessel)
                {
                    Destroy(p.gameObject);
                }
            }

            yield return new WaitForFixedUpdate();
            Debug.Log("[OrX Spawn Vessels Locally] Setting as isPersistent ..................");

            localVessel.isPersistent = true;
            localVessel.Landed = false;
            localVessel.situation = Vessel.Situations.FLYING;
            while (localVessel.packed)
            {
                yield return null;
            }
            localVessel.SetWorldVelocity(Vector3d.zero);
            localVessel.GoOffRails();
            localVessel.IgnoreGForces(240);
            StageManager.BeginFlight();
            Debug.Log("[OrX Spawn Vessels Locally] Beginning flight ..................");

            localVessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, true);
            OrXHoloKron.instance.triggerVessel = FlightGlobals.ActiveVessel;
            if (!isGoal)
            {

                OrXVesselMove.Instance.StartMove(localVessel, true, _altToSubtract, false);
            }
            else
            {
                Debug.Log("[OrX Spawn Vessels Locally] Beginning flight ..................");

                Quaternion _fixRot = Quaternion.identity;
                localVessel.IgnoreGForces(240);
                localVessel.angularVelocity = Vector3.zero;
                localVessel.angularMomentum = Vector3.zero;
                localVessel.SetWorldVelocity(Vector3d.zero);

                _fixRot = Quaternion.FromToRotation(localVessel.ReferenceTransform.up, UpVect) * localVessel.ReferenceTransform.rotation;
                localVessel.SetRotation(_fixRot, true);
                float localAlt = Convert.ToSingle(localVessel.radarAltitude);
                Debug.Log("[OrX Spawn Local Vessels] === PLACING GOAL ===");
                localAlt -= _altToSubtract;
                float dropRate = Mathf.Clamp((localAlt * 2), 0.1f, 200);

                while (!localVessel.LandedOrSplashed)
                {
                    localVessel.IgnoreGForces(240);
                    localVessel.angularVelocity = Vector3.zero;
                    localVessel.angularMomentum = Vector3.zero;
                    localVessel.SetWorldVelocity(Vector3.zero);
                    dropRate = Mathf.Clamp((localAlt * 2), 0.1f, 200);

                    if (dropRate > 3)
                    {
                        localVessel.Translate(dropRate * Time.fixedDeltaTime * -UpVect);
                    }
                    else
                    {
                        localVessel.SetWorldVelocity(dropRate * -UpVect);
                    }

                    if (localAlt <= 0.5f)
                    {
                        localAlt = 0.5f;
                    }

                    localAlt -= dropRate * Time.fixedDeltaTime;

                    yield return new WaitForFixedUpdate();
                }
                if (isGoal)
                {
                    Debug.Log("[OrX Spawn Local Vessels] === GOAL PLACED ===");

                    if (post1spawned)
                    {
                        Debug.Log("[OrX Spawn Local Vessels] === SPANING GOAL POST #2 ===");

                        StartCoroutine(SpawnVesselLocally(craftFile, goalCoords, true, stageStartCoords));
                    }
                    else
                    {
                        Debug.Log("[OrX Spawn Local Vessels] === HANDING OVER TO OrXHoloKron.instance.ChallengeAddNextCoord() ===");

                        OrXHoloKron.instance.ChallengeAddNextCoord();
                    }
                }
            }
        }

        public void CraftSelect()
        {
            StartCoroutine(StartVesselSelectRoutine());
        }
        public IEnumerator StartVesselSelectRoutine()
        {
            Debug.Log("[OrX Craft Select] Start craft selection");
            OrXHoloKron.instance.OrXHCGUIEnabled = false;
            OrXHoloKron.instance.movingCraft = true;
            OrXHoloKron.instance.openingCraftBrowser = true;
            yield return null;
            craftBrowser = CraftBrowserDialog.Spawn(EditorFacility.SPH, HighLogic.SaveFolder, OnSelected, OnCancelled, false);
        }
        public void OnSelected(string _selectedCraftFile, CraftBrowserDialog.LoadType loadType)
        {
            craftBrowser = null;
            openingCraftBrowser = false;
            Debug.Log("[OrX Craft Select] Selected Craft: " + _selectedCraftFile);

            if (spawningGoal)
            {
                Debug.Log("[OrX Craft Select] Start goal spawn");

                //Vector3d gpsPos = WorldPositionToGeoCoords(new Vector3d(_HoloKron.latitude, _HoloKron.longitude, _HoloKron.altitude), FlightGlobals.currentMainBody);
                //OrXSpawnHoloKron.instance.SpawnStartingGate();
            }
            else
            {
                if (OrXHoloKron.instance.addingBluePrints)
                {
                    ConfigNode _craftFile = ConfigNode.Load(_selectedCraftFile);
                    Debug.Log("[OrX Craft Select] SAVING BLUEPRINTS TO HOLOCACHE ............");

                    foreach (ConfigNode.Value cv in _craftFile.values)
                    {
                        if (cv.name == "ship")
                        {
                            OrXHoloKron.instance.craftToAddMission = cv.value;
                            break;
                        }

                        string cvEncryptedName = OrXLog.instance.Crypt(cv.name);
                        string cvEncryptedValue = OrXLog.instance.Crypt(cv.value);
                        cv.name = cvEncryptedName;
                        cv.value = cvEncryptedValue;
                    }

                    foreach (ConfigNode cn in _craftFile.nodes)
                    {
                        foreach (ConfigNode.Value cv in cn.values)
                        {
                            string cvEncryptedName = OrXLog.instance.Crypt(cv.name);
                            string cvEncryptedValue = OrXLog.instance.Crypt(cv.value);
                            cv.name = cvEncryptedName;
                            cv.value = cvEncryptedValue;
                        }

                        foreach (ConfigNode cn2 in cn.nodes)
                        {
                            foreach (ConfigNode.Value cv2 in cn2.values)
                            {
                                string cvEncryptedName = OrXLog.instance.Crypt(cv2.name);
                                string cvEncryptedValue = OrXLog.instance.Crypt(cv2.value);
                                cv2.name = cvEncryptedName;
                                cv2.value = cvEncryptedValue;
                            }
                        }
                    }
                    OrXHoloKron.instance.addingBluePrints = false;
                    OrXHoloKron.instance.blueprintsAdded = true;
                    OrXHoloKron.instance.blueprintsLabel = OrXHoloKron.instance.craftToAddMission;
                    OrXHoloKron.instance.spawningGoal = false;
                    OrXHoloKron.instance.blueprintsFile = _selectedCraftFile;
                    OrXHoloKron.instance.addingBluePrints = false;
                    OrXHoloKron.instance.movingCraft = false;
                    OrXHoloKron.instance.OrXHCGUIEnabled = true;
                    OrXHoloKron.instance.GuiEnabledOrXMissions = true;
                }
            }
        }
        public void OnCancelled()
        {
            Debug.Log("[OrX Craft Select] Cancelling Select Craft ............");

            OrXHoloKron.instance.GuiEnabledOrXMissions = true;
            OrXHoloKron.instance.movingCraft = false;
            OrXHoloKron.instance.spawningGoal = false;
            OrXHoloKron.instance.craftBrowser = null;
            OrXHoloKron.instance.openingCraftBrowser = false;
            OrXHoloKron.instance.OrXHCGUIEnabled = true;
        }

    }
}