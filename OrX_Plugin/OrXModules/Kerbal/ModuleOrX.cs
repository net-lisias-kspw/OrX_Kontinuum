﻿
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OrX
{
    public class ModuleOrX : PartModule, IPartMassModifier
    {
        #region Fields

        [KSPField(unfocusedRange = 1, isPersistant = false, guiActiveEditor = false, guiActive = false, guiName = "REVIVE"),
             UI_Toggle(controlEnabled = true, scene = UI_Scene.Flight, disabledText = "", enabledText = "")]
        public bool revive = false;

        [KSPField(isPersistant = true)]
        public bool infected = false;
        [KSPField(isPersistant = true)]
        public bool orx = false;
        [KSPField(isPersistant = true)]
        public bool helmetRemoved = false;

        public bool _narcosisCheck = false;
        public bool holdDepth = false;
        public bool holdingDepth = false;
        public bool narcosis = false;
        public bool bends = false;
        public bool unlockedScuba = true;
        public bool trimUp = false;
        public bool trimDown = false;

        private bool pilot = false;
        private bool engineer = false;
        private bool scientist = false;
        private bool civilian = false;

        [KSPField(isPersistant = true)]
        public float _walkSpeed = 0.0f;
        [KSPField(isPersistant = true)]
        public float _runSpeed = 0.0f;
        [KSPField(isPersistant = true)]
        public float _strafeSpeed = 0.0f;
        [KSPField(isPersistant = true)]
        public float _maxJumpForce = 0.0f;
        [KSPField(isPersistant = true)]
        public float _swimSpeed = 0.0f;
        [KSPField(isPersistant = true)]
        public float oxygenMax = 100.0f;
        [KSPField(isPersistant = true)]
        public float oxygen = 100.0f;
        [KSPField(isPersistant = true)]
        public float trimModifier = 1;
        public float _trimModifier = 1;
        private float massModifier = 0.0f;

        [KSPField(isPersistant = true)]
        public double _scubaLevel = 1;
        [KSPField(isPersistant = true)]
        public double _bendsDepth = 0;
        public double hoverAlt = 2;

        float localScale = 0;

        Rigidbody _rb;
        double mPerDegree = 0;
        double degPerMeter = 0;

        #endregion

//        [KSPField(unfocusedRange = 25, guiActiveUnfocused = true, isPersistant = false, guiActiveEditor = false, guiActive = true, guiName = "POPCORN"),
//UI_Toggle(controlEnabled = true, scene = UI_Scene.Flight, disabledText = "", enabledText = "")]
        public bool _popcorn = false;

//        [KSPField(unfocusedRange = 25, guiActiveUnfocused = true, isPersistant = false, guiActiveEditor = false, guiActive = true, guiName = "CHASE TEST"),
//     UI_Toggle(controlEnabled = true, scene = UI_Scene.Flight, disabledText = "", enabledText = "")]
        public bool _chase = false;

//        [KSPField(unfocusedRange = 25, guiActiveUnfocused = true, isPersistant = false, guiActiveEditor = false, guiActive = true, guiName = "SAVE ENCRYPTED"),
//UI_Toggle(controlEnabled = true, scene = UI_Scene.Flight, disabledText = "", enabledText = "")]
        public bool saveEncrypted = false;

        public enum AnimationState
        {
            None,
            Bounds,
            Idle,
            Walk,
            Run,
            Swim
        }

        private KerbalEVA kerbal;
        private KerbalEVA kerbalControl()
        {
            KerbalEVA kControl = part.FindModuleImplementing<KerbalEVA>();
            return kControl;
        }

        Vector3 _localScale;

        public override void OnStart(StartState state)
        {
            this.part.force_activate();
            if (!orx)
            {
                //OrXLog.instance.AddToVesselList(this.vessel);
            }
            kerbal = kerbalControl();
            //_rb = null;
            _maxJumpForce = kerbal.maxJumpForce;
            _walkSpeed = kerbal.walkSpeed;
            _runSpeed = kerbal.runSpeed;
            _strafeSpeed = kerbal.strafeSpeed;
            _swimSpeed = kerbal.swimSpeed;
            unlockedScuba = true;
            trimModifier = _trimModifier;
            forward = this.part.transform.forward;
            _localScale = this.part.transform.localScale;
            if (spawn.OrXSpawnHoloKron.instance.spawning)
            {
                kerbal.characterColliders.Initialize();
            }
            mPerDegree = (((2 * (FlightGlobals.ActiveVessel.mainBody.Radius + FlightGlobals.ActiveVessel.altitude)) * Math.PI) / 360);
            degPerMeter = 1 / mPerDegree;
            base.OnStart(state);
        }

        private bool chasing = false;

        public void Update()
        {
            if (!FlightGlobals.ready || PauseMenu.isOpen || !vessel.loaded || vessel.HoldPhysics)
                return;

            if (HighLogic.LoadedSceneIsFlight)
            {
                if (!bends)
                {
                    if (!orx)
                    {
                        if (_chase)
                        {
                            orx = true;
                        }

                        if (FlightGlobals.currentMainBody.atmosphereContainsOxygen)
                        {
                            if (this.vessel.Splashed)
                            {
                                if (!this.vessel.isEVA)
                                {
                                    massModifier = 0;
                                }
                                else
                                {
                                    if (!drunk)
                                    {
                                        if (vessel.isActiveVessel)
                                        {
                                            CheckScuba();
                                        }
                                    }
                                    else
                                    {
                                        warningLight = true;
                                        _wlFlash = false;
                                    }

                                    if (warningLight)
                                    {
                                        if (!_wlFlash)
                                        {
                                            //this.vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, true);
                                        }
                                    }
                                }

                                if (this.vessel.altitude <= -1)
                                {
                                    oxygen -= 0.005f;
                                }
                                else
                                {
                                    if (oxygen <= 99.9f)
                                    {
                                        oxygen += 0.1f;
                                    }
                                    else
                                    {
                                        oxygen = 100;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (_chase)
                        {
                            if (chasing)
                            {

                            }
                        }
                        else
                        {
                            if (this.vessel.isActiveVessel)
                            {
                                //OrXLog.instance.CheckVesselList(this.vessel);
                            }
                            else
                            {
                                if (infected)
                                {
                                    if (!orxSetup)
                                    {
                                        orxSetup = true;
                                        //SetupOrXStats();
                                    }
                                }
                                else
                                {
                                    // SETUP DIALOGUE MENU ACCESS HERE
                                    if (pilot)
                                    {
                                        // ACCESS PILOT STORY
                                    }
                                    if (engineer)
                                    {
                                        // ACCESS ENGINEER STORY
                                    }
                                    if (scientist)
                                    {
                                        // YADA
                                    }
                                    if (civilian)
                                    {
                                        // YADA
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();

            if (HighLogic.LoadedSceneIsFlight && this.vessel.loaded)
            {
                if (saveEncrypted)
                {
                    saveEncrypted = false;
                    SaveEncrypted();
                }

                if (_popcorn)
                {
                    _popcorn = false;
                    PopKorn();
                }
                if (_chase && orx)
                {
                    if (!chasing)
                    {
                        chasing = true;
                        kerbal.splatEnabled = false;
                        GetTarget(true);
                        StartCoroutine(GetDistance());
                    }
                }

                if (revive)
                {
                    Revive();
                }

                if (massModifier <= 0)
                {
                    massModifier = 0;
                }
                else
                {
                    if (massModifier >= 20)
                    {
                        massModifier = 20;
                    }
                }

                if (this.vessel.Splashed)
                {
                    SplashedCheck();
                }
                else
                {
                    _scubaLevel = 1;
                    martiniLevel = 0;
                    drunk = false;
                    revive = false;
                    narcosisCheck = false;
                }
            }
        }

        private void SaveEncrypted()
        {
            OrXLog.instance.DebugLog("[OrX Module - Save Encrypted] === SAVING OrX ===");

            Vessel toSave = FlightGlobals.ActiveVessel;
            string shipDescription = "Infected";

            ShipConstruct ConstructToSave = new ShipConstruct(toSave.vesselName, shipDescription, toSave.parts[0]);
            ConfigNode craftConstruct = new ConfigNode("craft");
            craftConstruct = ConstructToSave.SaveShip();

            craftConstruct.RemoveValue("persistentId");
            craftConstruct.RemoveValue("steamPublishedFileId");
            craftConstruct.RemoveValue("rot");
            craftConstruct.RemoveValue("missionFlag");
            craftConstruct.RemoveValue("vesselType");
            craftConstruct.RemoveValue("OverrideDefault");
            craftConstruct.RemoveValue("OverrideActionControl");
            craftConstruct.RemoveValue("OverrideAxisControl");
            craftConstruct.RemoveValue("OverrideGroupNames");

            foreach (ConfigNode cn in craftConstruct.nodes)
            {
                if (cn.name == "PART")
                {
                    cn.RemoveValue("persistentId");
                }
            }

            OrXLog.instance.DebugLog("[OrX Module - Save Encrypted] Saving: " + toSave.vesselName);
            OrXHoloKron.instance.OnScrnMsgUC("<color=#cfc100ff><b>Saving: " + toSave.vesselName + "</b></color>");
            craftConstruct.Save(UrlDir.ApplicationRootPath + "GameData/OrX/Plugin/PluginData/VesselData/OrX/" + toSave.vesselName + ".craft");

            // ADD ENCRYPTION

            foreach (ConfigNode.Value cv in craftConstruct.values)
            {
                if (cv.name == "ship")
                {
                    cv.value = toSave.vesselName;
                }

                string cvEncryptedName = OrXLog.instance.Crypt(cv.name);
                string cvEncryptedValue = OrXLog.instance.Crypt(cv.value);
                cv.name = cvEncryptedName;
                cv.value = cvEncryptedValue;
            }

            foreach (ConfigNode cn in craftConstruct.nodes)
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
                        if (!cv2.value.Contains("(") && !cv2.value.Contains(")"))
                        {
                            string cvEncryptedName = OrXLog.instance.Crypt(cv2.name);
                            string cvEncryptedValue = OrXLog.instance.Crypt(cv2.value);
                            cv2.name = cvEncryptedName;
                            cv2.value = cvEncryptedValue;
                        }
                    }
                }
            }
            craftConstruct.Save(UrlDir.ApplicationRootPath + "GameData/OrX/Plugin/PluginData/VesselData/OrX/" + toSave.vesselName + ".crypt");
            //toSave.rootPart.AddModule("ModuleOrXJason", true);
        }

        private void CheckScuba()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (!holdDepth)
                {
                    massModifier = 0.4f;
                    holdDepth = true;
                    OrXHoloKron.instance.OnScrnMsgUC("Holding depth at " + Convert.ToInt32(this.vessel.altitude) + " meters");
                }
                else
                {
                    holdDepth = false;
                    holdingDepth = false;
                }
            }

            if (Input.GetKeyDown(KeyCode.Z))
                massModifier = 0;

            if (Input.GetKeyDown(KeyCode.X))
                massModifier += 10;

            if (Input.GetKeyDown(KeyCode.E))
                massModifier += 0.4f;

            if (Input.GetKeyDown(KeyCode.Q))
                massModifier -= 0.4f;
        }

       
        void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject != this.gameObject && !collision.gameObject.name.Contains("Kerbin"))
            {
                if (_chase && targetDistance <= 2)
                {
                    OrXHoloKron.instance.salt += part.mass;
                    OrX_KC.instance.salt += part.mass;
                    OrX_KC.instance.victimCount -= 1;
                    this.part.explode();
                }
            }
        }
        
       
        IEnumerator GetDistance()
        {
            if (chasing)
            {
                targetDistance = OrXUtilities.instance.GetDistance(FlightGlobals.ActiveVessel.longitude, FlightGlobals.ActiveVessel.latitude, vessel.longitude, vessel.latitude, (FlightGlobals.ActiveVessel.altitude + vessel.altitude) / 2);
                yield return new WaitForFixedUpdate();
                StartCoroutine(GetDistance());
                RunAway();
            }
        }

        double targetDistance = 0;
        Vector3d targetCoords;

        public void GetTarget(bool _run)
        {
            if (_run)
            {
                targetCoords = new Vector3d(FlightGlobals.ActiveVessel.latitude, FlightGlobals.ActiveVessel.longitude, vessel.altitude);
            }
        }

        public void RunAway()
        {
            float _vel = TimeWarp.fixedDeltaTime;
            Vector3 _currLoc = FlightGlobals.ActiveVessel.mainBody.GetWorldSurfacePosition((double)vessel.latitude, (double)vessel.longitude, (double)vessel.altitude);
            Vector3 _targetLoc = FlightGlobals.ActiveVessel.mainBody.GetWorldSurfacePosition((double)targetCoords.x, (double)targetCoords.y, (double)vessel.altitude);
            Vector3 _rotty = (_currLoc - _targetLoc).normalized;
            Quaternion _lookTo = Quaternion.LookRotation((_rotty * _vel), kerbal.fUp);
            Quaternion _rotTo = Quaternion.RotateTowards(vessel.transform.rotation, _lookTo, kerbal.turnRate);
            kerbal.part.vessel.SetRotation(_rotTo);
            UpdateAnims();
            _rb = vessel.GetComponent<Rigidbody>();

            if (targetDistance <= 50)
            {
                if (targetDistance <= 10)
                {
                    int _right = UnityEngine.Random.Range(0, 100);

                    if (_right >= 50)
                    {
                        _rb.velocity = vessel.ReferenceTransform.right * 15;
                    }
                    else
                    {
                        _rb.velocity = -vessel.ReferenceTransform.right * 15;
                    }

                    GetTarget(true);
                }
                else
                {
                    _rb.MovePosition(_rb.position + _rotty * kerbal.runSpeed * Time.fixedDeltaTime);

                    //_rb.velocity = vessel.ReferenceTransform.up * kerbal.runSpeed * Time.fixedDeltaTime;
                }
            }
            else
            {
                _rb.MovePosition(_rb.position + _rotty * kerbal.walkSpeed * Time.fixedDeltaTime);

                //_rb.velocity = vessel.ReferenceTransform.up * kerbal.walkSpeed * Time.fixedDeltaTime;
            }
        }


        private void Revive()
        {
            if (!bends)
            {
                if (drunk && this.vessel.Splashed)
                {
                    _scubaLevel += 0.01f;
                    drunk = false;
                }
            }
            else
            {
                int r = new System.Random().Next(1, 10);
                if (r >= 2)
                {
                    if (r >= 8)
                    {
                        this.vessel.Translate((this.vessel.ReferenceTransform.position - this.vessel.mainBody.transform.position).normalized * 2);
                    }
                    else
                    {
                        StartCoroutine(PopKornRevival());
                    }
                }
                else
                {
                    this.part.explosionPotential *= 0.2f;
                    this.part.explode();
                }
            }
        }
        private void SplashedCheck()
        {
            if (this.vessel.isActiveVessel)
            {
                OrXScubaKerbGUI.instance.drunk = drunk;
                OrXScubaKerbGUI.instance.oxygen = oxygen;
                OrXScubaKerbGUI.instance.martiniLevel = martiniLevel;
            }

            if (this.vessel.altitude <= narcosisDepth * _scubaLevel)
            {
                _scubaLevel += 0.0001f;

                if (_bendsDepth >= this.vessel.altitude)
                {
                    _bendsDepth = this.vessel.altitude;
                }
            }

            if (_scubaLevel >= 1.00002f)
            {
                if (this.vessel.altitude >= (_bendsDepth * 0.667f) * _scubaLevel)
                {
                    bends = true;
                }
                else
                {
                    if (this.vessel.altitude >= _bendsDepth * 0.9f)
                    {
                        _bendsDepth += 0.1f;
                        _scubaLevel -= 0.00001f;
                    }
                }
            }

            if (!bends)
            {
                if (!holdingDepth && holdDepth)
                {
                    holdingDepth = true;
                    depth = this.vessel.altitude;
                    StartCoroutine(DepthCheck());
                }

                martiniLevel = this.vessel.altitude / (narcosisDepth * _scubaLevel);
                if (!narcosisCheck)
                {
                    narcosisCheck = true;
                    StartCoroutine(NarcosisCheck());
                }

                if (drunk)
                {
                    if (!drunkTank)
                    {
                        drunkTank = true;
                    }

                    if (this.vessel.isActiveVessel && !OrXLog.instance._EVALockWS)
                    {
                        OrXLog.instance.EVALockWS();
                        kerbal = kerbalControl();
                        kerbal.canRecover = false;
                        kerbal.isRagdoll = true;
                        MiniMe();
                    }

                    if (!this.vessel.Landed)
                    {
                        this.part.transform.Rotate(new Vector3(42, 42, 42) * Time.fixedDeltaTime);
                    }
                }
                else
                {
                    if (drunkTank)
                    {
                        drunkTank = false;

                        if (this.vessel.isActiveVessel && OrXLog.instance._EVALockWS)
                        {
                            OrXLog.instance.EVAUnlockWS();
                        }
                        kerbal = kerbalControl();
                        kerbal.canRecover = true;
                        kerbal.isRagdoll = false;
                    }
                }
            }
            else
            {
                if (this.vessel.rootPart.Modules.Contains<KerbalEVA>() && _scubaLevel >= 1.0002f)
                {
                    if (!poppingKorn)
                    {
                        poppingKorn = true;
                        PopKorn();
                    }
                }
            }
        }

        bool poppingKorn = false;

        private void PopKorn()
        {
            bends = true;
            holdingDepth = false;
            holdDepth = false;
            massModifier = 0;
            kerbal = kerbalControl();
            kerbal.canRecover = false;
            kerbal.isRagdoll = true;
            this.vessel.UpdateCaches();
            StartCoroutine(PopGoesTheKerbal());
        }

        IEnumerator PopGoesTheKerbal()
        {
            if (this.part.transform.localScale.x <= _localScale.x * 2)
            {
                yield return new WaitForSeconds(1);
                this.part.transform.localScale += new Vector3(0.01f, 0.01f, 0.01f);
                StartCoroutine(PopGoesTheKerbal());
            }
            else
            {
                int r = new System.Random().Next(1, 10);
                if (r >= 2)
                {
                    this.part.explosionPotential *= 0.2f;
                    this.part.explode();
                }
            }
        }
        IEnumerator PopKornRevival()
        {
            if (this.part.transform.localScale.x >= _localScale.x)
            {
                yield return new WaitForSeconds(1);
                this.part.transform.localScale += new Vector3(-0.01f, -0.01f, -0.01f);
                StartCoroutine(PopKornRevival());
            }
        }
        private void MiniMe()
        {
            bends = false;
            //holdingDepth = false;
            //holdDepth = false;
            //massModifier = 0;
            kerbal = kerbalControl();
            kerbal.canRecover = false;
            kerbal.isRagdoll = true;
            this.vessel.UpdateCaches();
            //massModifier = 5;
            StartCoroutine(MiniMeKerbal());
        }
        IEnumerator MiniMeKerbal()
        {
            if (this.part.transform.localScale.x >= _localScale.x)
            {
                yield return new WaitForSeconds(1);
                this.part.transform.localScale += new Vector3(-0.01f, -0.01f, -0.01f);
                StartCoroutine(MiniMeKerbal());
            }
        }

        //////////////////////////////////////////////////////////////////////////////

        #region OrX Jet Pack

        Rigidbody rigidBody;
        double hoverHeight = 2;
        bool hover = false;


        #endregion

        //////////////////////////////////////////////////////////////////////////////

        #region Scuba

        bool narcosisCheck = false;

        [KSPField(isPersistant = true)]
        public bool drunk = false;

        [KSPField(isPersistant = true)]
        public bool drunkTank = false;

        [KSPField(isPersistant = true)]
        public double martiniLevel = 0;

        double narcosisDepth = -20;
        double depthCheck = 0;
        double p1 = 0;
        double p2 = 0;
        private bool bendsTimer = false;
        double depth = 0;
        double timeAtDepth = 0;
        bool bendsCheck = false;
        bool narcosisHoldDepth = false;
        bool warningLight = false;
        bool _wlFlash = false;

        IEnumerator DepthCheck()
        {
            if (holdDepth)
            {
                if (this.vessel.altitude >= depth)
                {
                    if (_bendsDepth - (narcosisDepth * 2.5f) >= this.vessel.altitude)
                    {
                        if (massModifier <= 1)
                        {
                            massModifier += 0.1f;
                        }
                    }
                }
                else
                {
                    if (this.vessel.altitude <= depth)
                    {
                        if (this.vessel.altitude >= depth + narcosisDepth)
                        {
                            if (massModifier >= 0)
                            {
                                massModifier -= 0.1f;
                            }
                        }
                        else
                        {
                            holdDepth = false;
                            massModifier = 0.5f;
                        }
                    }
                }

                yield return new WaitForSeconds(0.5f);

                StartCoroutine(DepthCheck());
            }
            else
            {
                holdingDepth = false;
                OrXHoloKron.instance.OnScrnMsgUC("Releasing hold on depth");
            }
        }
        IEnumerator NarcosisCheck()
        {
            if (this.vessel.Splashed)
            {
                kerbal = kerbalControl();

                if (oxygen <= 0)
                {
                    oxygen = 0;
                    if (this.vessel.isActiveVessel)
                    {
                        OrXLog.instance.EVALockWS();
                    }
                    kerbal.canRecover = false;
                    kerbal.isRagdoll = true;
                }
                else
                {
                    if (martiniLevel >= 1)
                    {
                        if (martiniLevel >= 2.7)
                        {
                            if (martiniLevel >= 4.5)
                            {
                                warningLight = true;
                                _wlFlash = false;

                                if (martiniLevel >= 6)
                                {
                                    drunk = true;
                                    kerbal.canRecover = false;
                                    kerbal.isRagdoll = true;
                                }
                                else
                                {
                                    if (martiniLevel >= 5.3)
                                    {
                                        depth += 0.01f;
                                        if (!holdDepth)
                                        {
                                            holdDepth = true;
                                            holdingDepth = false;
                                        }
                                    }
                                    kerbal.canRecover = true;
                                    yield return new WaitForSeconds(1);
                                }
                            }
                            else
                            {
                                warningLight = true;
                                _wlFlash = false;
                                yield return new WaitForSeconds(1);
                                _wlFlash = true;
                                drunk = false;
                                kerbal.isRagdoll = false;
                                kerbal.canRecover = true;
                                yield return new WaitForSeconds(1);
                            }
                        }
                        else
                        {
                            warningLight = true;
                            _wlFlash = false;
                            yield return new WaitForSeconds(1);
                            _wlFlash = true;
                            kerbal.canRecover = true;
                            drunk = false;
                            yield return new WaitForSeconds(1);
                        }
                    }
                    else
                    {
                        warningLight = false;
                        kerbal.canRecover = true;
                        drunk = false;
                        yield return new WaitForSeconds(1);
                    }
                }
            }
            else
            {
                warningLight = false;
                drunk = false;
            }
            _wlFlash = false;
            narcosisCheck = false;
        }

        #endregion

        //////////////////////////////////////////////////////////////////////////////

        #region Kerbal

        //////////////////////////////////////////////////////////////////////////////
        // KERBAL UPDATES
        //////////////////////////////////////////////////////////////////////////////

        private bool orxSetup = false;

        private void SetupOrXStats()
        {
            OrXLog.instance.DebugLog("[Module OrX] SETUP ORX STATS ========================================");

            //var kerbal = this.part.FindModuleImplementing<KerbalEVA>();
            kerbal.maxJumpForce = _maxJumpForce * 0.5f;
            kerbal.walkSpeed = _walkSpeed * 0.7f;
            kerbal.runSpeed = _runSpeed * 0.7f;
            kerbal.strafeSpeed = _strafeSpeed;
            kerbal.swimSpeed = _swimSpeed * 0.7f;
            kerbal.lampOn = false;
            helmetRemoved = true;
        }
        public void ResetOrXStats()
        {
            OrXLog.instance.DebugLog("[Module OrX] RESET ORX STATS ========================================");

            //var kerbal = this.part.FindModuleImplementing<KerbalEVA>();
            kerbal.maxJumpForce = _maxJumpForce;
            kerbal.walkSpeed = _walkSpeed;
            kerbal.runSpeed = _runSpeed;
            kerbal.strafeSpeed = _strafeSpeed;
            kerbal.swimSpeed = _swimSpeed;
        }

        public void AnimState(AnimationState state)
        {
            string _animState = "idle";

            if (kerbal.part.WaterContact)
            {
                _animState = "swim_idle";
            }
            else if (kerbal.JetpackDeployed)
            {
                _animState = "jp_suspended";
            }

            switch (state)
            {
                case AnimationState.Swim: { _animState = "swim_forward"; } break;
                case AnimationState.Run: { _animState = "wkC_run"; } break;
                case AnimationState.Walk: { _animState = "wkC_forward"; } break;
                case AnimationState.Bounds: { _animState = "wkC_Log_forward"; } break;
                case AnimationState.Idle: { _animState = "idle"; } break;
            }

            Animation _anim = null;
            kerbal.GetComponentCached<Animation>(ref _anim);
            if (_anim != null)
            {
                _anim.CrossFade(_animState);
            }
        }
        public void UpdateAnims()
        {
            if (vessel.srfSpeed == 0)
            {
                AnimState(AnimationState.Idle);
            }
            else
            {
                if (kerbal.part.WaterContact && !vessel.Landed)
                {
                    AnimState(AnimationState.Swim);
                }
                else if (kerbal.JetpackDeployed)
                {
                    AnimState(AnimationState.Idle);
                }
                else if (FlightGlobals.currentMainBody.GeeASL >= kerbal.minRunningGee && vessel.srfSpeed >= kerbal.walkSpeed)
                {
                    AnimState(AnimationState.Run);
                }
                else if (FlightGlobals.currentMainBody.GeeASL >= kerbal.minWalkingGee)
                {
                    AnimState(AnimationState.Walk);
                }
                else
                {
                    if (targetDistance >= 50)
                    {
                        AnimState(AnimationState.Bounds);
                    }
                }
            }
        }


        //////////////////////////////////////////////////////////////////////////////

        #endregion

        //////////////////////////////////////////////////////////////////////////////

        #region HELMET
        //////////////////////////////////////////////////////////////////////////////
        // HELMET
        //////////////////////////////////////////////////////////////////////////////

        private void RemoveHelmet()
        {
            OrXLog.instance.DebugLog("[Module OrX] REMOVING HELMET ========================================");
            var kerbal = this.part.FindModuleImplementing<KerbalEVA>();
            kerbal.lampOn = false;
            helmetRemoved = true;
        }

        private void ShowHelmet()
        {
            var kerbal = this.part.FindModuleImplementing<KerbalEVA>();
            kerbal.lampOn = false;
            helmetRemoved = false;
        }

        #endregion

        //////////////////////////////////////////////////////////////////////////////

        #region Snowball

        Vector3 forward;


        private void SpawnSnowball()
        {

        }

        #endregion

        //////////////////////////////////////////////////////////////////////////////

        #region Core

        public void setMassModifier(float massModifier)
        {
            this.massModifier = massModifier;
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            return defaultMass * massModifier;
        }

        public ModifierChangeWhen GetModuleMassChangeWhen()
        {
            return ModifierChangeWhen.CONSTANTLY;
        }

        #endregion

    }

}
