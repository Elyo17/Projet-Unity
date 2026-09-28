using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]

public class ProceduralMeshCreation : MonoBehaviour
{
    private Mesh p_mesh;
    private Vector3[] p_vertices;
    private int[] p_triangles;
    private Vector3[] p_normals;

    public float width = 1.0f;

    Vector3 p0, p1, p2, p3, p4, p5, p6, p7;

   


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        p_mesh = new Mesh();
        p_mesh.name = "MyProceduralCube";
        // définir les coordonnées(vector3) des 8 sommets du Cube
        // chaque sommet est traduit par des vertices non partagés entre les triangles de faces différentes
        // chaque face = 1 quad donc 2 triangles , chaque face = 4 vertices pour former les 2 triangles
        float w = -width / 2.0f;
        float W = width / 2.0f;
        p0 = new Vector3(w, w, w);
        p1 = new Vector3(w, W, w);
        p2 = new Vector3(W, W, w);
        p3 = new Vector3(W, w, w);
        p4 = new Vector3(w, w, W);
        p5 = new Vector3(w, W, W);
        p6 = new Vector3(W, W, W);
        p7 = new Vector3(W, w, W);
        // les 24 vertices non partagés du cube
        // 6 faces (devant, gauche, etc ..) , chacune constituée de 4 points
        // ici les vertices d'une face à l'autre ne sont pas partagés
        // par exemple : le vecteur p0 qui représente la position d'un sommet est ajouté 3 fois
        // p0 est la position d'un vertex de la face devant
        // p0 est la position d'un autre vertex de la face gauche
        // p0 est la position d'un vertex de la face devant

        p_vertices = new Vector3[]{
             p0,p1,p2,p3, // devant
             p4,p5,p1,p0, // gauche
             p3,p2,p6,p7, // Droite
             p7,p6,p5,p4, // Derrière
             p1,p5,p6,p2, // Dessus
             p4,p0,p3,p7 // dessous
        };
        p_normals = new Vector3[p_vertices.Length];
        // les indices des 3 vertices des 12 triangles (2 pour chacune des 6 faces du cube)
        p_triangles = new int[12 * 3];
        int index = 0;
        for (int i = 0; i < 6; i++) // 6 faces à 2 triangles
        { // triangle 1
            p_triangles[index++] = i * 4;
            p_triangles[index++] = i * 4 + 1;
            p_triangles[index++] = i * 4 + 3;
            // triangle 2
            p_triangles[index++] = i * 4 + 1;
            p_triangles[index++] = i * 4 + 2;
            p_triangles[index++] = i * 4 + 3;
        }


        //calcul des normales : initialiser un remplir un tableau de 12x3 normales, une pour chaque vertex
        p_normals = new Vector3[p_vertices.Length];
        Vector3 v1, v2, pv;
        for (int i = 0; i < 6; i++)
        { // on traite une à une chacune des 6 faces
            v1 = p_vertices[i * 4 + 1] - p_vertices[i * 4 + 0];
            v2 = p_vertices[i * 4 + 2] - p_vertices[i * 4 + 0];
            pv = Vector3.Cross(v1, v2);
            pv = pv / pv.magnitude; // une normale par face
                                    // les 4 vertices de la face (des 2 triangles) ont la même normale
            p_normals[i * 4 + 0] = pv;
            p_normals[i * 4 + 1] = pv;
            p_normals[i * 4 + 2] = pv;
            p_normals[i * 4 + 3] = pv;


        }

        p_mesh.vertices = p_vertices;
        p_mesh.triangles = p_triangles;
        p_mesh.normals = p_normals;
        GetComponent<MeshFilter>().mesh = p_mesh;
    }

    // Update is called once per frame
    void Update()
    {
        DebugNormals(true, true, true);
    }


    private void DebugNormals(bool affN_Orientation, bool affN_Eclairage, bool affN_vertices)
    {

        if (affN_Orientation)
        {
            Vector3 vposmoyenne, v1, v2, vnormetri;
            for (int num_tri = 0; num_tri < p_triangles.Length; num_tri += 3)
            { // calcul de vnormetri comme le produit vectoriel de V01^V02
                vposmoyenne = p_vertices[p_triangles[num_tri]] + p_vertices[p_triangles[num_tri + 1]] +
                p_vertices[p_triangles[num_tri + 2]];
                vposmoyenne /= 3.0f;
                v1 = p_vertices[p_triangles[num_tri + 1]] - p_vertices[p_triangles[num_tri + 0]];
                v2 = p_vertices[p_triangles[num_tri + 2]] - p_vertices[p_triangles[num_tri + 0]];
                vnormetri = Vector3.Cross(v1, v2);
                Debug.DrawRay(transform.position + vposmoyenne, vnormetri.normalized * 3.0f, Color.yellow, 5,
               false);
            }
        }
        if (affN_Eclairage)
        {
            Vector3 vposmoyenne, vnormmoyenne;
            for (int num_tri = 0; num_tri < p_triangles.Length; num_tri += 3)
            {
                vposmoyenne = (p_vertices[p_triangles[num_tri]] + p_vertices[p_triangles[num_tri + 1]] +
                p_vertices[p_triangles[num_tri + 2]]) / 3.0f;
                vnormmoyenne = (p_normals[p_triangles[num_tri]] + p_normals[p_triangles[num_tri + 1]] +
                p_normals[p_triangles[num_tri + 2]]) / 3.0f;
                Debug.DrawRay(transform.position + vposmoyenne, vnormmoyenne.normalized * 2.0f, Color.green, 5, false);
            }
        }
        if (affN_vertices)
        {
            for (int num_vert = 0; num_vert < p_vertices.Length; num_vert++)
                Debug.DrawRay(transform.position + p_vertices[num_vert], p_normals[num_vert].normalized * 1.0f, Color.red,
               5, false);
        }
    }

}
