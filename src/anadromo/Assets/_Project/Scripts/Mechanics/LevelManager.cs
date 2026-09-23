using UnityEngine;
using Anadromo.Mechanics;

namespace Anadromo.Logic
{
    public enum GamePhase {
        Init,
        KrillFeeding,
        AbysmDescent,
        OrcaAscent,
        BloopAwakening
    }

    [AddComponentMenu("Anadromo/Logic/Level Manager")]
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        public GamePhase currentPhase = GamePhase.Init;

        [Header("Referencias a Depredadores")]
        public SwimGroupController salmonesLeft;
        public SwimGroupController salmonesRight;
        public SwimGroupController salmonesTop;
        public SharkGroupMovement orcaGroup;
        
        public BloopFishMovement bloopMovement;

        [Header("Referencias a Krills (Spawners)")]
        public Transform krillNormalLeft;
        public Transform krillNormalRight1;
        public Transform krillNormalRight2;
        
        public Transform krillScaryLeft;
        public Transform krillScaryRight;
        public Transform krillScaryMid;

        [Header("Referencias Adicionales")]
        public PlayerFeeding playerFeeding;
        public Transform topReference; // Referencia hacia arriba para orcas
        
        [Header("Configuración de Eventos")]
        public int playerEatenCount = 0;
        public float firstLimitBloop = -50f;
        public float secondLimitBloop = -20f;
        
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (playerFeeding != null)
            {
                playerFeeding.OnPreyConsumed.AddListener(() => {
                    playerEatenCount++;
                    CheckOrcaAscentCondition();
                });
            }
        }

        // ================= EVENTOS DE TRIGGERS ================= //

        // 1. Cuando el jugador sale de la zona inicial
        public void OnPlayerLeaveInitZone()
        {
            if (currentPhase == GamePhase.Init)
            {
                currentPhase = GamePhase.KrillFeeding;
                
                // Salmones atacan a los normales
                if (salmonesLeft) salmonesLeft.GoToTarget(krillNormalLeft);
                if (salmonesRight) salmonesRight.GoToTarget(krillNormalRight1);
                if (salmonesTop) salmonesTop.GoToTarget(krillNormalRight2);
                
                Debug.Log("Fase iniciada: KrillFeeding (Salmones atacan krills normales)");
            }
        }

        // 2. Cuando el jugador se come todos los krills del medio (Trigger externo)
        public void OnMidKrillsDepleted()
        {
            if (currentPhase == GamePhase.KrillFeeding)
            {
                currentPhase = GamePhase.AbysmDescent;
                
                // Salmones atacan a los scary
                if (salmonesLeft) salmonesLeft.GoToTarget(krillScaryLeft);
                if (salmonesRight) salmonesRight.GoToTarget(krillScaryRight);
                
                // salmonesTop bajan a ayudar a comer a los de la derecha (ya que los del medio son solo para el jugador)
                if (salmonesTop) salmonesTop.GoToTarget(krillScaryRight);

                Debug.Log("Fase iniciada: AbysmDescent (Salmones atacan krills scary left/right)");
            }
        }

        // 3. Cuando el jugador entra al abismo del medio
        public bool isPlayerInAbysm = false;
        public void OnPlayerEnterAbysm()
        {
            isPlayerInAbysm = true;
            CheckOrcaAscentCondition();
        }

        public void CheckOrcaAscentCondition()
        {
            if (currentPhase == GamePhase.AbysmDescent && isPlayerInAbysm && playerEatenCount >= 5)
            {
                currentPhase = GamePhase.OrcaAscent;
                
                // Salmones desesperados
                SetDesperateMode(salmonesLeft);
                SetDesperateMode(salmonesRight);
                SetDesperateMode(salmonesTop);
                
                // Orcas suben
                if (orcaGroup != null)
                {
                    orcaGroup.IniciarGrupo();
                }
                
                Debug.Log("Fase iniciada: OrcaAscent (Salmones huyen/aceleran y Orcas suben)");
            }
        }

        // 4. Cuando el jugador entra al límite de la cueva
        public void OnPlayerEnterCave()
        {
            // Chequeo de orcas arriba
            if (currentPhase == GamePhase.OrcaAscent && CheckOrcasAboveLimit())
            {
                currentPhase = GamePhase.BloopAwakening;
                if (bloopMovement != null) bloopMovement.IniciarMovimiento();
                
                Debug.Log("Fase iniciada: BloopAwakening (Bloop sube)");
            }
        }

        private void Update()
        {
            if (currentPhase == GamePhase.BloopAwakening && bloopMovement != null)
            {
                float bloopY = bloopMovement.transform.position.y;
                
                if (bloopY >= firstLimitBloop && bloopY < secondLimitBloop)
                {
                    // Lógica para temblar cámara
                    Debug.Log("Bloop subiendo: temblar cámara");
                }
                else if (bloopY >= secondLimitBloop)
                {
                    // Caída de rocas y visión negra
                    Debug.Log("Bloop subió: caída de rocas");
                    enabled = false; 
                }
            }
        }

        private void SetDesperateMode(SwimGroupController group)
        {
            if (group == null) return;
            group.swimSpeed *= 1.7f;
            BoxObjectSpawner spawner = group.GetComponent<BoxObjectSpawner>();
            if (spawner != null)
            {
                spawner.size *= 1.5f;
            }
        }
        
        private bool CheckOrcasAboveLimit()
        {
            // Como las orcas usan SharkGroupMovement y suben hacia 'puntoFin', 
            // basta con verificar si alguna superó el límite
            if (orcaGroup != null)
            {
                foreach (Transform child in orcaGroup.transform)
                {
                    if (child.position.y >= topReference.position.y) 
                        return true;
                }
            }
            return false;
        }
    }
}
