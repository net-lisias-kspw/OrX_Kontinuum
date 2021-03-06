using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OrX.spawn
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class OrXVesselMove : MonoBehaviour
    {
        public static OrXVesselMove Instance;
        Vector3d targetPosition;
        double _distance = 0;

        #region Variables

        public enum MoveModes { Slow = 0, Normal = 1, Ludicrous = 2 }
        public MoveModes _moveMode = MoveModes.Slow;
        private bool _moving = false;
        public List<Vessel> _placingVessels = new List<Vessel>();
        private bool _hoverChanged;
        public float MoveHeight = 0;
        private float _hoverAdjust = 0f;
        public float MoveSpeed = 25;
        public float MoveAccel = 15;
        private readonly float[] _rotationSpeeds = new float[] {5, 25, 50 };
        private float RotationSpeed
        {
            get
            {
                return _rotationSpeeds[(int)_moveMode] * Time.fixedDeltaTime;
            }
        }
        Rigidbody _rb;
        public bool IsMovingVessel = false;
        public Vessel MovingVessel;
        private Quaternion _startRotation;
        private Quaternion _currRotation;
        private float _currMoveSpeed = 0;
        private Vector3 _currMoveVelocity;
        private VesselBounds _vBounds;
        private LineRenderer _lineRender;
        private Vector3 UpVect;
        private Vector3 _startingUp;
        private readonly float maxPlacementSpeed = 1050;
        private bool _hasRotated = false;
        private float _timeBoundsUpdated = 0;
        private ScreenMessage _moveMessage;
        public bool placingGate = false;

        public Vector3 offsetDirection = Vector3.zero;
        public bool _externalControl = false;
        public float _moveSpeed = 0;
        public Vector3 forward;
        public Vector3 right;

        bool spawningLocal = false;
        double degPerMeter = 0;
        double mPerDegree = 0;
        float _alt = 0;

        #endregion

        #region Core

        private void Awake()
        {
            if (Instance)
            {
                Destroy(Instance);
            }
            Instance = this;
        }
        private void Start()
        {
            _lineRender = new GameObject().AddComponent<LineRenderer>();
            _lineRender.material = new Material(Shader.Find("KSP/Emissive/Diffuse"));
            _lineRender.material.SetColor("_EmissiveColor", XKCDColors.AquaGreen);
            _lineRender.startWidth = 0.05f;
            _lineRender.endWidth = 0.05f;
            _lineRender.enabled = false;
        }
        private void Update()
        {
            if (_moving && MovingVessel != null)
            {
                int circleRes = 24;
                Vector3[] positions = new Vector3[circleRes + 3];
                for (int i = 0; i < circleRes; i++)
                {
                    positions[i] = GetBoundPoint(i, circleRes, 1);
                }
                positions[circleRes] = GetBoundPoint(0, circleRes, 1);
                positions[circleRes + 1] = MovingVessel.CoM;
                positions[circleRes + 2] = MovingVessel.CoM + (MoveHeight * -UpVect);
                _lineRender.positionCount = circleRes + 3;
                _lineRender.SetPositions(positions);
            }
        }
        private void FixedUpdate()
        {
            if (!_moving) return;
            if (MovingVessel == null)
            {
                _moving = false;
                MoveHeight = 0;
                _hoverAdjust = 0f;
                MovingVessel = new Vessel();
            }
            MovingVessel.IgnoreGForces(240);
            UpdateMove();

            if (_hasRotated && Time.time - _timeBoundsUpdated > 0.2f)
            {
                //UpdateBounds();
            }
        }


        #endregion

        #region Utilities

        private Vector3 GetBoundPoint(int index, int totalPoints, float radiusFactor)
        {
            float angleIncrement = 360 / (float)totalPoints;

            float angle = index * angleIncrement;

            Vector3 forward = North();//Vector3.ProjectOnPlane((movingVessel.CoM)-FlightCamera.fetch.mainCamera.transform.position, up).normalized;

            float radius = 1;

            Vector3 offsetVector = (radius * radiusFactor * forward);
            offsetVector = Quaternion.AngleAxis(angle, UpVect) * offsetVector;

            Vector3 point = MovingVessel.CoM + offsetVector;

            return point;
        }
        private bool CapsuleCast(out RaycastHit rayHit)
        {
            //float radius = (Mathf.Max (Mathf.Max(vesselBounds.size.x, vesselBounds.size.y), vesselBounds.size.z)) + (currMoveSpeed*2);
            float radius = _alt + Mathf.Clamp(_currMoveSpeed, 0, 200);

            return Physics.CapsuleCast(MovingVessel.CoM + (250 * UpVect), MovingVessel.CoM + (249 * UpVect), radius, -UpVect, out rayHit, 2000, 1 << 15);
        }
        private Vector3 North()
        {
            Vector3 n = MovingVessel.mainBody.GetWorldSurfacePosition(MovingVessel.latitude + 1, MovingVessel.longitude, MovingVessel.altitude) - MovingVessel.GetWorldPos3D();
            n = Vector3.ProjectOnPlane(n, UpVect);
            return n.normalized;
        }
        public struct VesselBounds
        {
            public Vessel vessel;
            public float BottomLength;
            public float Radius;

            private Vector3 _localBottomPoint;
            public Vector3 BottomPoint
            {
                get
                {
                    return vessel.transform.TransformPoint(_localBottomPoint);
                }
            }

            public VesselBounds(Vessel v)
            {
                vessel = v;
                BottomLength = 0;
                Radius = 0;
                _localBottomPoint = Vector3.zero;
                UpdateBounds();
            }

            public void UpdateBounds()
            {
                Vector3 up = (vessel.CoM - vessel.mainBody.transform.position).normalized;
                Vector3 forward = Vector3.ProjectOnPlane(vessel.CoM - FlightCamera.fetch.mainCamera.transform.position, up).normalized;
                Vector3 right = Vector3.Cross(up, forward);

                float maxSqrDist = 0;
                Part furthestPart = null;

                //bottom check
                Vector3 downPoint = vessel.CoM - (2000 * up);
                Vector3 closestVert = vessel.CoM;
                float closestSqrDist = Mathf.Infinity;

                //radius check
                Vector3 furthestVert = vessel.CoM;
                float furthestSqrDist = -1;

                foreach (Part p in vessel.parts)
                {
                    if (p.Modules.Contains("ModuleRobotArmScanner")) return;
                    if (p.Modules.Contains("ModuleScienceExperiment")) return;
                    if (p.Modules.Contains("Tailhook")) return;
                    if (p.Modules.Contains("Arrestwire")) return;
                    if (p.Modules.Contains("Catapult")) return;
                    if (p.Modules.Contains("CLLS")) return;
                    if (p.Modules.Contains("OLS")) return;

                    float sqrDist = Vector3.ProjectOnPlane((p.transform.position - vessel.CoM), up).sqrMagnitude;
                    if (sqrDist > maxSqrDist)
                    {
                        maxSqrDist = sqrDist;
                        furthestPart = p;
                    }

                    //if(Vector3.Dot(up, p.transform.position-vessel.CoM) < 0)
                    //{

                    foreach (MeshFilter mf in p.GetComponentsInChildren<MeshFilter>())
                    {
                        //Mesh mesh = mf.mesh;
                        foreach (Vector3 vert in mf.mesh.vertices)
                        {
                            //bottom check
                            Vector3 worldVertPoint = mf.transform.TransformPoint(vert);
                            float bSqrDist = (downPoint - worldVertPoint).sqrMagnitude;
                            if (bSqrDist < closestSqrDist)
                            {
                                closestSqrDist = bSqrDist;
                                closestVert = worldVertPoint;
                            }

                            //radius check
                            //float sqrDist = (vessel.CoM-worldVertPoint).sqrMagnitude;
                            float hSqrDist = Vector3.ProjectOnPlane(vessel.CoM - worldVertPoint, up).sqrMagnitude;
                            if (!(hSqrDist > furthestSqrDist)) continue;
                            furthestSqrDist = hSqrDist;
                            furthestVert = worldVertPoint;
                        }
                    }

                    //}
                }

                Vector3 radVector = Vector3.ProjectOnPlane(furthestVert - vessel.CoM, up);
                Radius = radVector.magnitude;

                BottomLength = Vector3.Project(closestVert - vessel.CoM, up).magnitude;
                _localBottomPoint = vessel.transform.InverseTransformPoint(closestVert);

                //OrXLog.instance.DebugLog ("Vessel bottom length: "+bottomLength);
                /*
                        if(furthestPart!=null)
                        {
                            //OrXLog.instance.DebugLog ("Furthest Part: "+furthestPart.partInfo.title);

                            Vector3 furthestVert = vessel.CoM;
                            float furthestSqrDist = -1;

                            foreach(var mf in furthestPart.GetComponentsInChildren<MeshFilter>())
                            {
                                Mesh mesh = mf.mesh;
                                foreach(var vert in mesh.vertices)
                                {
                                    Vector3 worldVertPoint = mf.transform.TransformPoint(vert);
                                    float sqrDist = (vessel.CoM-worldVertPoint).sqrMagnitude;
                                    if(sqrDist > furthestSqrDist)
                                    {
                                        furthestSqrDist = sqrDist;
                                        furthestVert = worldVertPoint;
                                    }
                                }
                            }

                            Vector3 radVector = Vector3.ProjectOnPlane(furthestVert-vessel.CoM, up);
                            radius = radVector.magnitude;
                            //OrXLog.instance.DebugLog ("Vert test found radius to be "+radius);
                        }
                        */
                //radius *= 1.75f;
                //radius += 5;//15;
                Radius += Mathf.Clamp(Radius, 2, 10);
            }


        }
        public static List<string> partIgnoreModules = new List<string>(9)
        {
        "ModuleRobotArmScanner",
        "ModuleScienceExperiment",
            "Tailhook",
            "Arrestwire",
            "Catapult",
            "CLLS",
            "OLS"
        };
        private static bool IsPartModuleIgnored(string ModuleName)
        {
            return true;
        }

        #endregion

        public void SetAltitude(bool _raise, float _alt)
        {
            _lineRender.enabled = false;
            MovingVessel.ActionGroups.SetGroup(KSPActionGroup.Gear, true);
            _alt += (float)MovingVessel.altitude;
            StartCoroutine(SetAltitudeRoutine(_raise, _alt));
        }
        IEnumerator SetAltitudeRoutine(bool _raise, float _alt)
        {
            MovingVessel.ActionGroups.SetGroup(KSPActionGroup.Gear, false);

            if (_raise)
            {
                if (MovingVessel.altitude <= _alt)
                {
                    MovingVessel.IgnoreGForces(240);

                    MoveHeight += (float)MovingVessel.radarAltitude / 80;
                    yield return new WaitForFixedUpdate();
                    StartCoroutine(SetAltitudeRoutine(_raise, _alt));
                }
                else
                {
                    OrXHoloKron.instance._spawningVessel = true;
                    OrXHoloKron.instance.getNextCoord = true;
                }
            }
            else
            {
                if (MovingVessel.altitude >= _alt)
                {
                    MoveHeight -= (float)MovingVessel.radarAltitude / 100;
                    yield return new WaitForFixedUpdate();
                    StartCoroutine(SetAltitudeRoutine(_raise, _alt));
                }
                else
                {
                    OrXHoloKron.instance._spawningVessel = true;
                    OrXHoloKron.instance.getNextCoord = true;
                }
            }
        }
        public void StartMove(Vessel v, bool _spawningLocal, float _altitude, bool _placingGate, bool _external, Vector3d _targetPos)
        {
            if (!_external)
            {
                _lineRender.enabled = true;
            }
            OrXLog.instance.SetFocusKeys();
            targetPosition = _targetPos;
            _externalControl = _external;
            MovingVessel = new Vessel();
            placingGate = _placingGate;
            _alt = _altitude;
            spawningLocal = _spawningLocal;
            _moveMode = MoveModes.Normal;
            MovingVessel = v;
            IsMovingVessel = true;
            _distance = OrXUtilities.instance.GetDistance(MovingVessel.longitude, MovingVessel.latitude, _targetPos.y, _targetPos.x, (MovingVessel.altitude + _targetPos.z) / 2);// CheckDistance(null, targetPosition);

            if (MovingVessel == OrXHoloKron.instance._HoloKron)
            {
                MoveHeight = 10;
            }
            else
            {
                MoveHeight = 10 + _alt;
            }
            mPerDegree = (((2 * (FlightGlobals.ActiveVessel.mainBody.Radius + FlightGlobals.ActiveVessel.altitude)) * Math.PI) / 360);
            degPerMeter = 1 / mPerDegree;

            UpVect = (v.transform.position - v.mainBody.transform.position).normalized;
            _startRotation = MovingVessel.transform.rotation;
            _currRotation = _startRotation;
            _rb = MovingVessel.GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _moving = true;
            if (_placingGate)
            {
                OrXHoloKron.instance.spawningStartGate = true;
                OrXHoloKron.instance.movingCraft = true;
                OrXHoloKron.instance._spawningVessel = false;
                OrXHoloKron.instance.getNextCoord = true;
                OrXHoloKron.instance.OrXHCGUIEnabled = true;
            }
            else
            {
                if (_external)
                {
                    OrXHoloKron.instance.Reach();
                }
                else
                {
                    OrXHoloKron.instance._spawningVessel = true;
                    OrXHoloKron.instance.getNextCoord = true;
                    OrXHoloKron.instance.OrXHCGUIEnabled = true;
                }
            }

            if (MovingVessel != FlightGlobals.ActiveVessel)
            {
                Debug.Log("[OrX Vessel Move] ===== SETTING ACTIVE VESSEL =====");
                FlightGlobals.ForceSetActiveVessel(MovingVessel);
            }
        }
        private void UpdateMove()
        {
            if (!_moving) return;
            MovingVessel.IgnoreGForces(240);
            // Lerp is animating move
            if (!_hoverChanged)
            {
                //MoveHeight = Mathf.Lerp(MoveHeight, Convert.ToSingle(MovingVessel.radarAltitude), 10 * Time.fixedDeltaTime);// + HoverHeight, 10 * Time.fixedDeltaTime);
            }
            else
            {
                _alt = MoveHeight;
                if (MoveHeight < _alt) MoveHeight = (float)_alt;
                if (MoveHeight > _alt) MoveHeight = (float)_alt;
                MoveHeight = Mathf.Lerp(MoveHeight, (float)_alt + _hoverAdjust, 10 * Time.fixedDeltaTime);
            }
            MovingVessel.ActionGroups.SetGroup(KSPActionGroup.RCS, false);

            _hoverAdjust = 0;

            if (_externalControl)
            {
                if (MovingVessel.radarAltitude >= MovingVessel.altitude)
                {
                    MoveHeight = (float)MovingVessel.radarAltitude - (float)MovingVessel.altitude;
                }

                if (MoveHeight <= 10)
                {
                    MoveHeight = 10;
                }

                if (_distance >= 15 && _moving)
                {
                    if (OrXUtilities.instance.GetDistance(MovingVessel.longitude, MovingVessel.latitude, targetPosition.y, targetPosition.x, (MovingVessel.altitude + targetPosition.z) / 2) >= _distance * 0.5)
                    {
                        MoveHeight += 0.1f;
                    }
                    else
                    {
                        MoveHeight -= 0.1f;
                    }
                }
            }

            if (MoveHeight <= 10)
            {
                MoveSpeed = (float)MovingVessel.radarAltitude / 3;
            }
            else
            {
                if (MoveHeight >= 50)
                {
                    if (MoveHeight >= 100)
                    {
                        _lineRender.material.SetColor("_EmissiveColor", XKCDColors.BluePurple);
                        MoveSpeed = 100 + ((float)MovingVessel.radarAltitude / 10);
                    }
                    else
                    {
                        _lineRender.material.SetColor("_EmissiveColor", XKCDColors.AquaGreen);
                        MoveSpeed = (float)MovingVessel.radarAltitude;
                    }
                }
                else
                {
                    MoveSpeed = (float)MovingVessel.radarAltitude / 2;
                }
            }
            UpVect = (MovingVessel.transform.position - FlightGlobals.currentMainBody.transform.position).normalized;
            right = Vector3.Cross(UpVect, forward);

            offsetDirection = Vector3.zero;
            bool inputting = false;
            _moveSpeed = 0;

            //Altitude Adjustment

            _moveSpeed = MoveSpeed;

            if (!_externalControl)
            {
                if (MapView.MapIsEnabled)
                {
                    forward = North();
                }
                else
                {
                    forward = Vector3.ProjectOnPlane(MovingVessel.CoM - FlightCamera.fetch.mainCamera.transform.position, UpVect).normalized;
                    if (Vector3.Dot(-UpVect, FlightCamera.fetch.mainCamera.transform.up) > 0)
                    {
                        forward = Vector3.ProjectOnPlane(FlightCamera.fetch.mainCamera.transform.up, UpVect).normalized;
                    }
                }

                if (placingGate)
                {
                    if (MoveHeight >= 20)
                    {
                        MoveHeight = 20;
                    }
                }

                if (GameSettings.PITCH_DOWN.GetKey())
                {
                    offsetDirection += (forward * _moveSpeed * Time.fixedDeltaTime);
                    inputting = true;
                }
                if (GameSettings.PITCH_UP.GetKey())
                {
                    offsetDirection += (-forward * _moveSpeed * Time.fixedDeltaTime);
                    inputting = true;
                }

                if (GameSettings.YAW_RIGHT.GetKey())
                {
                    offsetDirection += (right * _moveSpeed * Time.fixedDeltaTime);
                    inputting = true;
                }
                if (GameSettings.YAW_LEFT.GetKey())
                {
                    offsetDirection += (-right * _moveSpeed * Time.fixedDeltaTime);
                    inputting = true;
                }

                if (GameSettings.THROTTLE_UP.GetKey())
                {
                    if (MovingVessel.radarAltitude <= 3000)
                    {
                        if (MovingVessel.altitude - MovingVessel.radarAltitude <= 0)
                        {
                            MoveHeight += (float)MovingVessel.altitude / 100;
                        }
                        else
                        {
                            MoveHeight += (float)MovingVessel.radarAltitude / 100;
                        }
                    }
                }
                if (GameSettings.THROTTLE_DOWN.GetKey())
                {
                    if (MovingVessel.radarAltitude >= 1)
                    {
                        if (MovingVessel.altitude - MovingVessel.radarAltitude <= 0)
                        {
                            MoveHeight -= (float)MovingVessel.altitude / 100;
                        }
                        else
                        {
                            MoveHeight -= (float)MovingVessel.radarAltitude / 100;
                        }
                    }
                }

                if (GameSettings.TRANSLATE_RIGHT.GetKey())
                {
                    _startRotation = Quaternion.AngleAxis(-RotationSpeed, MovingVessel.ReferenceTransform.forward) * _startRotation;
                    _hasRotated = true;
                }
                else if (GameSettings.TRANSLATE_LEFT.GetKey())
                {
                    _startRotation = Quaternion.AngleAxis(RotationSpeed, MovingVessel.ReferenceTransform.forward) * _startRotation;
                    _hasRotated = true;
                }

                if (GameSettings.TRANSLATE_DOWN.GetKey())
                {
                    _startRotation = Quaternion.AngleAxis(RotationSpeed, MovingVessel.ReferenceTransform.right) * _startRotation;
                    _hasRotated = true;
                }
                else if (GameSettings.TRANSLATE_UP.GetKey())
                {
                    _startRotation = Quaternion.AngleAxis(-RotationSpeed, MovingVessel.ReferenceTransform.right) * _startRotation;
                    _hasRotated = true;
                }

                if (GameSettings.ROLL_LEFT.GetKey())
                {
                    _startRotation = Quaternion.AngleAxis(RotationSpeed, MovingVessel.ReferenceTransform.up) * _startRotation;
                    _hasRotated = true;
                }
                else if (GameSettings.ROLL_RIGHT.GetKey())
                {
                    _startRotation = Quaternion.AngleAxis(-RotationSpeed, MovingVessel.ReferenceTransform.up) * _startRotation;
                    _hasRotated = true;
                }
            }
            else
            {
                forward = Vector3.ProjectOnPlane(MovingVessel.CoM - OrXHoloKron.instance.targetLoc, UpVect).normalized;
                offsetDirection += (-forward * _moveSpeed * Time.fixedDeltaTime);
            }

            if (spawningLocal)
            {
            }

            if (inputting || _externalControl)
            {
                _currMoveSpeed = Mathf.Clamp(Mathf.MoveTowards(_currMoveSpeed, _moveSpeed, MoveAccel * Time.fixedDeltaTime), 0, MoveSpeed);
            }
            else
            {
                _currMoveSpeed = 0;
            }

            Vector3 offset = offsetDirection.normalized * _currMoveSpeed;
            _currMoveVelocity = offset / Time.fixedDeltaTime;
            Vector3 vSrfPt = MovingVessel.CoM - (MoveHeight * UpVect);
            RaycastHit ringHit;

            bool surfaceDetected = CapsuleCast(out ringHit);
            Vector3 finalOffset = Vector3.zero;

            Vector3 rOffset = Vector3.Project(ringHit.point - vSrfPt, UpVect);
            Vector3 mOffset = (vSrfPt + offset) - MovingVessel.CoM;
            finalOffset = rOffset + mOffset + (MoveHeight * UpVect);
            if (!_moving) return;
            MovingVessel.Translate(finalOffset);

            PQS bodyPQS = MovingVessel.mainBody.pqsController;

            Vector3d geoCoords = OrXUtilities.instance.WorldPositionToGeoCoords(MovingVessel.GetWorldPos3D() + (_currMoveVelocity * Time.fixedDeltaTime), MovingVessel.mainBody);
            double lat = geoCoords.x;
            double lng = geoCoords.y;

            Vector3d bodyUpVector = new Vector3d(1, 0, 0);
            bodyUpVector = QuaternionD.AngleAxis(lat, Vector3d.forward/*around Z axis*/) * bodyUpVector;
            bodyUpVector = QuaternionD.AngleAxis(lng, Vector3d.down/*around -Y axis*/) * bodyUpVector;

            double srfHeight = bodyPQS.GetSurfaceHeight(bodyUpVector);

            if (!surfaceDetected)
            {
                Vector3 terrainPos = MovingVessel.mainBody.position + (float)srfHeight * UpVect;

                if (!surfaceDetected)
                {
                    MovingVessel.SetPosition(terrainPos + (MoveHeight * UpVect) + offset);
                }
            }

            //fix surface rotation
            Quaternion srfRotFix = Quaternion.FromToRotation(_startingUp, UpVect);
            _currRotation = srfRotFix * _startRotation;
            MovingVessel.SetRotation(_currRotation);

            if (Vector3.Angle(_startingUp, UpVect) > 5)
            {
                _startRotation = _currRotation;
                _startingUp = UpVect;
            }
            MovingVessel.SetWorldVelocity(Vector3d.zero);

            if (_externalControl)
            {
                double _dist = OrXUtilities.instance.GetDistance(MovingVessel.longitude, MovingVessel.latitude, targetPosition.y, targetPosition.x, (MovingVessel.altitude + targetPosition.z) / 2);
                
                if (OrXHoloKron.instance.triggerVessel != MovingVessel)
                {
                    if (_dist <= MoveHeight)
                    {
                        KillMove(true, false);
                    }
                }
                else
                {
                    if (_dist <= MoveHeight)
                    {
                        if (OrXHoloKron.instance._placingChallenger)
                        {
                            _moving = false;
                            _externalControl = false;
                            MovingVessel.SetWorldVelocity(Vector3.zero);
                            MovingVessel.IgnoreGForces(240);
                            spawningLocal = false;
                            OrXLog.instance.DebugLog("[OrX Vessel Move] ===== KILLING MOVE =====");
                            _lineRender.enabled = false;
                            IsMovingVessel = false;
                            MoveHeight = 0;
                            _hoverAdjust = 0f;
                            MovingVessel = new Vessel();
                            OrXHoloKron.instance._holdVesselPos = false;
                            _rb = MovingVessel.GetComponent<Rigidbody>();
                            _rb.isKinematic = true;
                            _rb = null;
                            OrXLog.instance.ResetFocusKeys();
                            OrXHoloKron.instance.OpenScoreboardMenu();
                        }
                        else
                        {
                            KillMove(true, false);
                        }
                    }
                }
            }
        }

        public void KillMove(bool _place, bool _gate)
        {
            OrXHoloKron.instance._gateKillDelay = false;
            StartCoroutine(KillMoveRoutine(_place, _gate));
        }
        IEnumerator KillMoveRoutine(bool _place, bool _gate)
        {
            MovingVessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, true);
            _moving = false;
            _externalControl = false;
            //MovingVessel.SetWorldVelocity(Vector3.zero);
            MovingVessel.IgnoreGForces(240);
            spawningLocal = false;
            OrXLog.instance.DebugLog("[OrX Vessel Move] ===== KILLING MOVE =====");
            _lineRender.enabled = false;
            IsMovingVessel = false;
            MoveHeight = 0;
            _hoverAdjust = 0f;
            UpVect = (MovingVessel.transform.position - FlightGlobals.currentMainBody.transform.position).normalized;
            if (OrXHoloKron.instance._settingAltitude)
            {
                MovingVessel.rootPart.AddModule("ModuleHoldVessel");
                var _holdModule = MovingVessel.rootPart.FindModuleImplementing<ModuleHoldVessel>();
                _holdModule.altitude = MovingVessel.altitude;
                _holdModule.latitude = MovingVessel.latitude;
                _holdModule.longitude = MovingVessel.longitude;
            }

            yield return new WaitForFixedUpdate();

            if (_place && MovingVessel != OrXHoloKron.instance._HoloKron)
            {
                OrXLog.instance.DebugLog("[OrX Place] === PLACING " + MovingVessel.vesselName + " ===");
                bool _continue = true;
                while (!MovingVessel.LandedOrSplashed && _continue)
                {
                    MovingVessel.IgnoreGForces(240);
                    MovingVessel.SetWorldVelocity(Vector3.zero);
                    MovingVessel.Translate((float)MovingVessel.radarAltitude * 0.3f * Time.fixedDeltaTime * -UpVect);
                    yield return new WaitForFixedUpdate();
                    if (MovingVessel.radarAltitude <= 0.5f)
                    {
                        _continue = false;
                        MovingVessel.Landed = true;
                    }
                }
            }

            if (MovingVessel != OrXHoloKron.instance._HoloKron)
            {
                _rb = MovingVessel.GetComponent<Rigidbody>();
                _rb.isKinematic = false;
                _rb = null;
            }
            if (_gate)
            {
                OrXLog.instance.SetGoalRange(MovingVessel);
                OrXHoloKron.instance.ChallengeAddNextCoord();
            }
            else
            {
                if (OrXHoloKron.instance.triggerVessel != MovingVessel)
                {
                    if (OrXHoloKron.instance.buildingMission)
                    {
                        OrXHoloKron.instance.PlaceMenu();
                    }
                    else
                    {
                        if (OrXHoloKron.instance._spawnByOrX)
                        {
                            FlightGlobals.ForceSetActiveVessel(OrXHoloKron.instance.triggerVessel);
                            OrXHoloKron.instance.movingCraft = false;
                        }
                        else
                        {
                            FlightGlobals.ForceSetActiveVessel(OrXHoloKron.instance.triggerVessel);
                            if (OrXHoloKron.instance.bdaChallenge)
                            {
                                OrXHoloKron.instance.SpawnMenu();
                            }
                        }
                    }
                    OrXLog.instance.ResetFocusKeys();
                }
                else
                {
                    if (!OrXHoloKron.instance.bdaChallenge)
                    {
                        OrXLog.instance.ResetFocusKeys();
                        OrXHoloKron.instance.OpenScoreboardMenu();
                    }
                    else
                    {

                    }
                }
            }
            MovingVessel = new Vessel();
        }
        public void EndMove(bool addingCoords, bool _spawningLocal, bool _gate)
        {
            spawningLocal = _spawningLocal;
            OrXLog.instance.DebugLog("[OrX Vessel Move] ===== ENDING MOVE =====");
            StartCoroutine(EndMoveRoutine(addingCoords, _spawningLocal, _gate));
        }
        private IEnumerator EndMoveRoutine(bool addingCoords, bool _spawningLocal, bool _gate)
        {
            _lineRender.enabled = false;
            _placingVessels.Add(MovingVessel);
            float altitude = _alt;
            IsMovingVessel = false;
            _moving = false;
            UpVect = (MovingVessel.transform.position - FlightGlobals.currentMainBody.transform.position).normalized;
            float localAlt = Convert.ToSingle(MovingVessel.radarAltitude);
            yield return new WaitForFixedUpdate();
            float dropRate = Mathf.Clamp((localAlt * 1.5f), 0.1f, 200);

            if ((MovingVessel != OrXHoloKron.instance._HoloKron && !OrXHoloKron.instance._holdVesselPos) || MovingVessel == OrXHoloKron.instance.triggerVessel)
            {
                OrXLog.instance.DebugLog("[OrX Vessel Move] === PLACING " + MovingVessel.name + " ===");

                while (!MovingVessel.LandedOrSplashed)
                {
                    MovingVessel.IgnoreGForces(240);
                    MovingVessel.SetWorldVelocity(Vector3.zero);
                    dropRate = Mathf.Clamp((localAlt / 4), 0.1f, 200);

                    if (dropRate > 3)
                    {
                        MovingVessel.Translate(dropRate * Time.fixedDeltaTime * -UpVect);
                    }
                    else
                    {
                        if (dropRate <= 1f)
                        {
                            dropRate = 1f;
                        }

                        MovingVessel.SetWorldVelocity(dropRate * -UpVect);
                    }

                    localAlt -= dropRate * Time.fixedDeltaTime;
                    yield return new WaitForFixedUpdate();
                }
            }

            MovingVessel.SetWorldVelocity(Vector3.zero);
            MovingVessel.IgnoreGForces(240);
            _placingVessels.Remove(MovingVessel);
            MoveHeight = 0;
            _hoverAdjust = 0f;
            Vector3d vect = new Vector3d(MovingVessel.latitude, MovingVessel.longitude, MovingVessel.altitude);

            if (_spawningLocal)
            {
                spawningLocal = false;

                if (OrXHoloKron.instance._holdVesselPos || _gate)
                {
                    OrXHoloKron.instance.PlaceCraft();
                }
                else
                {
                    MovingVessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, true);

                    if (MovingVessel == OrXHoloKron.instance.triggerVessel)
                    {
                        OrXHoloKron.instance.OrXHCGUIEnabled = false;
                        OrXHoloKron.instance.MainMenu();
                    }
                    else
                    {
                        OrXHoloKron.instance.PlaceCraft();
                    }
                }
            }
            else
            {
                yield return new WaitForFixedUpdate();

                if (!_externalControl)
                {
                    if (addingCoords)
                    {
                        if (_gate)
                        {
                            OrXSpawnHoloKron.instance.StartSpawn(vect, vect, true, false, false, OrXHoloKron.instance.HoloKronName, OrXHoloKron.instance.missionType);
                        }
                        else
                        {
                            //OrXHoloKron.instance.SpawnGoal();
                            if (!OrXHoloKron.instance.LBC)
                            {
                                OrXSpawnHoloKron.instance.StartSpawn(vect, vect, true, false, false, OrXHoloKron.instance.HoloKronName, OrXHoloKron.instance.missionType);
                            }
                            else
                            {
                                OrXHoloKron.instance.ChallengeAddNextCoord();
                            }
                        }
                    }
                    else
                    {
                        OrXHoloKron.instance.addingMission = true;
                        OrXHoloKron.instance.SaveConfig("", false);
                    }
                }
                else
                {
                    _externalControl = false;
                    if (MovingVessel == OrXHoloKron.instance.triggerVessel)
                    {
                        OrXHoloKron.instance.OrXHCGUIEnabled = false;
                        OrXHoloKron.instance.MainMenu();
                    }
                    else
                    {
                        OrXHoloKron.instance.getNextCoord = true;
                    }
                }
            }
            if (_rb != null)
            {
                _rb = MovingVessel.GetComponent<Rigidbody>();
                _rb.isKinematic = false;
                _rb = null;
            }
            MovingVessel = new Vessel();
            OrXHoloKron.instance._holdVesselPos = false;
            OrXLog.instance.ResetFocusKeys();
        }
    }
}

