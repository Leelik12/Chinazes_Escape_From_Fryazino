using UnityEngine;
using Photon.Pun;
using System.Collections;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class VRMagazineSpawner : MonoBehaviourPun
{
    [Header("��������� ������")]
    [Tooltip("������ �������� (������ ������ � ����� Resources).")]
    [SerializeField] private GameObject magazinePrefab;

    [Tooltip("�����, ��� ���������� �������.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("�������� ����� ���������� ������ �������� (�������).")]
    [SerializeField] private float respawnDelay = 2f;

    [Tooltip("������������ ����������, � �������� �������� ������� ������� ������� �����.")]
    [SerializeField] private float checkRadius = 1f;

    private GameObject currentMagazine;
    private bool isSpawning = false;

    void Update()
    {
        // ������ MasterClient ��������� ��������� ��������� (����� �� ���� ����������)
        if (!PhotonNetwork.IsMasterClient) return;

        // ���������, ��� �� �������
        if (currentMagazine == null)
        {
            // ��������� ����� ������� ���������� (�� ������, ���� ���-�� ������ ������)
            Collider[] hits = Physics.OverlapSphere(spawnPoint.position, checkRadius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Ammo"))
                {
                    currentMagazine = hit.gameObject;
                    break;
                }
            }

            // ���� ��� � �� ����� � ������� �����
            if (currentMagazine == null && !isSpawning)
            {
                StartCoroutine(SpawnMagazineAfterDelay());
            }
        }
    }

    private IEnumerator SpawnMagazineAfterDelay()
    {
        isSpawning = true;
        yield return new WaitForSeconds(respawnDelay);

        if (magazinePrefab == null || spawnPoint == null)
        {
            Debug.LogWarning("VRMagazineSpawner: �� �������� prefab ��� ����� ������!");
            isSpawning = false;
            yield break;
        }

        // ������ ������� � ����
        GameObject newMag = PhotonNetwork.Instantiate(
            magazinePrefab.name,
            spawnPoint.position,
            spawnPoint.rotation
        );

        // ��������� Rigidbody
        Rigidbody rb = newMag.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // ��������� XRGrabInteractable
        var grab = newMag.GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            XRInteractionManager manager = FindObjectOfType<XRInteractionManager>();
            if (manager != null)
                grab.interactionManager = manager;

            grab.enabled = false;
            yield return null; // ��� 1 ����
            grab.enabled = true;
        }

        // ��������� ��������
        newMag.transform.SetParent(transform, true);

        // ��������� ���, ����� ������� ��� ��� ��������
        if (!newMag.CompareTag("Magazine"))
        {
            Debug.LogWarning("VRMagazineSpawner: � ���������� �������� ��� ���� 'Magazine'. �������� �������������.");
            newMag.tag = "Magazine";
        }

        currentMagazine = newMag;
        isSpawning = false;

        Debug.Log($"[VRMagazineSpawner] ������ ����� �������: {newMag.name}");
    }
}

