using System;
using UnityEngine;

namespace Rokas.Presentation
{
    // The character owns a stable socket; weapons remain independent reusable prefabs.
    public sealed class ReactiveCombatWeaponAttachment : MonoBehaviour
    {
        private Transform socket;
        private GameObject currentPrefab;
        private GameObject currentWeapon;
        public Transform Socket => socket;
        public GameObject CurrentWeapon => currentWeapon;

        public void Configure(Transform model, ReactiveCombatActorClips definition)
        {
            if (string.IsNullOrEmpty(definition.weaponBonePath)) return;
            Transform hand = model.Find(definition.weaponBonePath);
            if (hand == null) throw new InvalidOperationException("Weapon hand binding is missing: " + definition.weaponBonePath);
            socket = hand.Find("WeaponSocket_R");
            if (socket == null)
            {
                socket = new GameObject("WeaponSocket_R").transform;
                socket.SetParent(hand, false);
            }
            socket.localPosition = definition.weaponSocketPosition;
            socket.localRotation = Quaternion.Euler(definition.weaponSocketEuler);
            socket.localScale = definition.weaponSocketScale;
            Equip(definition.weaponPrefab);
        }

        public void Equip(GameObject prefab)
        {
            if (prefab == currentPrefab && (prefab == null || currentWeapon != null)) return;
            if (socket == null && prefab != null) throw new InvalidOperationException("Configure the weapon socket before equipping.");
            if (currentWeapon != null)
            {
                currentWeapon.SetActive(false);
                Destroy(currentWeapon);
            }
            currentPrefab = prefab;
            currentWeapon = prefab == null ? null : Instantiate(prefab, socket, false);
            if (currentWeapon == null) return;
            currentWeapon.name = prefab.name;
            currentWeapon.transform.localPosition = Vector3.zero;
            currentWeapon.transform.localRotation = Quaternion.identity;
            currentWeapon.transform.localScale = Vector3.one;
            // The arena camera isolates its actors on a dedicated layer.
            foreach (Transform child in currentWeapon.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = gameObject.layer;
        }
    }
}
