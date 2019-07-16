using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Kernel
{
    public int ID;
    public Vector3Int size;
    public string name;
    public Kernel(ComputeShader shader, string name)
    {
        this.name = name;
        ID = shader.FindKernel(name);
        uint x, y, z;
        shader.GetKernelThreadGroupSizes(ID, out x, out y, out z);
        size.x = (int)x;
        size.y = (int)y;
        size.z = (int)z;
    }
    public override string ToString()
    {
        return name + " groupSize=" + size.ToString();
    }
}
public static class ComputeProgramUtils
{
    public static void InitializeBuffer(ref ComputeBuffer buffer, int newCount, int stride)
    {
        if (buffer == null)
            buffer = new ComputeBuffer(newCount, stride);
        else if (buffer.count < newCount)
        {
            buffer.Release();
            buffer = new ComputeBuffer(newCount, stride);
        }
    }
    public static void DispatchGrid(this ComputeShader shader, Kernel kernel, int gridSizeX, int gridSizeY, int gridSizeZ)
    {
        /*if (gridSizeX / kernel.size.x < 1 || gridSizeY / kernel.size.y < 1 || gridSizeZ / kernel.size.z < 1)
            throw new ArgumentOutOfRangeException(String.Format("Grid size less than work group size: gridSize=({0}, {1}, {2}) {3}", gridSizeX, gridSizeY, gridSizeZ, kernel));
        if (gridSizeX % kernel.size.x > 0 || gridSizeY % kernel.size.y > 0 || gridSizeZ % kernel.size.z > 0)
            throw new ArgumentException(String.Format("Grid size don't divides by work group size: gridSize=({0}, {1}, {2}) {3}", gridSizeX, gridSizeY, gridSizeZ, kernel));
        shader.Dispatch(kernel.ID, gridSizeX / kernel.size.x, gridSizeY / kernel.size.y, gridSizeZ / kernel.size.z);
        */
        int X = (gridSizeX - 1) / kernel.size.x + 1;
        int Y = (gridSizeY - 1) / kernel.size.y + 1;
        int Z = (gridSizeZ - 1) / kernel.size.z + 1;
        //if (X != gridSizeX * kernel.size.x ||
        //    Y != gridSizeY * kernel.size.y ||
        //    Z != gridSizeZ * kernel.size.z)
        //    Debug.LogWarningFormat("Grid size donesn't alligned by work group size: gridSize=({0}, {1}, {2}) {3}", gridSizeX, gridSizeY, gridSizeZ, kernel);
        shader.Dispatch(kernel.ID, X, Y, Z);
    }
    public static void DispatchGrid(this ComputeShader shader, Kernel kernel, int gridSizeX, int gridSizeY)
    {
        int X = (gridSizeX - 1) / kernel.size.x + 1;
        int Y = (gridSizeY - 1) / kernel.size.y + 1;
        //if (X != gridSizeX * kernel.size.x ||
        //    Y != gridSizeY * kernel.size.y)
        //    Debug.LogWarningFormat("Grid size donesn't alligned by work group size: gridSize=({0}, {1}, 1) {2}", gridSizeX, gridSizeY, kernel);
        shader.Dispatch(kernel.ID, X, Y, 1);
    }
    public static void DispatchGrid(this ComputeShader shader, Kernel kernel, int gridSizeX)
    {
        int X = (gridSizeX - 1) / kernel.size.x + 1;
        //if (X != gridSizeX * kernel.size.x)
        //    Debug.LogWarningFormat("Grid size donesn't alligned by work group size: gridSize=({0}, 1, 1) {1}", gridSizeX, kernel);
        shader.Dispatch(kernel.ID, X, 1, 1);
    }
}