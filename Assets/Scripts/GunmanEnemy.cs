﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunmanEnemy : WandererEnemy
{
    //use timebetweenattacks and attack damage for shooting
    [SerializeField] float shootingRange = 20f;
    [SerializeField] float accuracyOffset = 5f;
    [SerializeField] float projectileLifetime = 2f;
    [SerializeField] float projectileSpeed = 10f;
    //[Range(0, 100)] [SerializeField] int chanceToShootOnSight = 20;

    [SerializeField] Transform gunTransform = default; //to look at player 
    [SerializeField] Transform shotPoint = default;
    [SerializeField] GameObject projectilePrefab = default;


    private bool isShooting = false;
    

    public override void Start()
    {
        base.Start();
        Physics2D.queriesStartInColliders = false;
    }

    public override void Update()
    {
        base.Update();

        Shoot();
    }

    private void Shoot()
    {
        if (playerTransform == null) { return; }

        Vector2 directionToPlayer = playerTransform.position - transform.position;

        RaycastHit2D hitInfo = Physics2D.Raycast(transform.position, directionToPlayer, shootingRange);

        //Player is on enemy's line of sight
        if (hitInfo.collider.gameObject.tag == "Player")
        {
            Debug.DrawLine(transform.position, hitInfo.point, Color.red);
            LookAtPlayer();
            if (!isShooting)
            {
                isShooting = true;
                StartCoroutine(ShootRoutine());
            }
        }
        else
        {
            Debug.DrawLine(transform.position, hitInfo.point, Color.green);
        }
    }

    private void LookAtPlayer()
    {
        var lookDirection = playerTransform.position - gunTransform.position;
        FacePlayer();
        gunTransform.right = lookDirection;
    }

    IEnumerator ShootRoutine()
    {
        GameObject projectile = Instantiate(projectilePrefab, shotPoint.position, gunTransform.rotation) as GameObject;

        if (projectile != null )
        {
            projectile.GetComponent<EnemyProjectile>().Init(attackDamage, projectileSpeed, projectileLifetime);
        }

        yield return new WaitForSeconds(timeBetweenAttacks);

        isShooting = false;
    }
}
