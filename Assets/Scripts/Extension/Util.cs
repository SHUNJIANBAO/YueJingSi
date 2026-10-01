using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class Util
{
    public static Type GetType(string typeName)
    {
        return Type.GetType(typeName);
    }

    /// <summary>
    /// 查找子物体
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="go"></param>
    /// <param name="childName"></param>
    /// <returns></returns>
    public static T GetChild<T>(GameObject go, string childName) where T : UnityEngine.Object
    {
        Transform trans = go.transform.Find(childName);
        if (trans == null) return null;
        if (typeof(T) == typeof(GameObject))
        {
            return trans.gameObject as T;
        }

        if (typeof(T) == typeof(Transform)) return trans as T;
        else
        {
            return trans.GetComponent<T>();
        }
    }

    /// <summary>
    /// 将组件按Y轴高低排序
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="array"></param>
    public static void SortForPositionY<T>(ref T[] array) where T : UnityEngine.Component
    {
        T tempValue = null;
        for (int i = 0; i < array.Length; i++)
        {
            for (int j = i + 1; j < array.Length; j++)
            {
                if (array[i].transform.position.y < array[j].transform.position.y)
                {
                    tempValue = array[i];
                    array[i] = array[j];
                    array[j] = tempValue;
                }
            }
        }
    }

    /// <summary>
    /// 将值转化成指定类型
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public static object GetValue(object obj, System.Type type, string propertyName)
    {
        string value = obj.ToString().Trim();
        if (string.IsNullOrEmpty(value))
        {
            if (type == typeof(int) || type == typeof(float) || type == typeof(double) ||
                type == typeof(uint) || type == typeof(byte) || type == typeof(sbyte) ||
                type == typeof(short) || type == typeof(long) || type == typeof(Int32) ||
                type == typeof(Int64))
            {
                return 0;
            }

            if (type == typeof(bool))
            {
                return false;
            }
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            string[] tempArry = value.Split(';',',');
            var memberType = type.GetGenericArguments()[0];
            var listType = typeof(List<>).MakeGenericType(new Type[] { memberType });
            var list = Activator.CreateInstance(listType, new object[] { });

            //List<string> tempList = new List<string>();
            for (int i = 0; i < tempArry.Length; i++)
            {
                if (string.IsNullOrEmpty(tempArry[i]))
                    continue;
                object addItem = GetValue(tempArry[i], memberType, "");
                list.GetType().InvokeMember("Add", BindingFlags.Default | BindingFlags.InvokeMethod, null, list,
                    new object[] { addItem });
                //tempList.Add(tempArry[i]);
            }

            //return tempList;
            return list;
        }

        if (type == typeof(bool))
        {
            float temp = float.Parse(value);
            if (temp != 0) return true;
            return false;
        }
        else if (type.BaseType == typeof(Enum))
            return GetValue(value, Enum.GetUnderlyingType(type), "");

        if (type == typeof(Vector3))
        {
            string[] tempArry = value.Split('|', ';', ':', ',');
            Vector3 temp;
            if (tempArry.Length < 3)
            {
                temp = Vector3.zero;
            }
            else
            {
                temp = new Vector3(float.Parse(tempArry[0]), float.Parse(tempArry[1]), float.Parse(tempArry[2]));
            }

            return temp;
        }

        if (type == typeof(Vector2))
        {
            string[] tempArry = value.Split('|', ';', ':', ',');
            Vector2 temp;
            if (tempArry.Length < 2)
            {
                temp = Vector2.zero;
            }
            else
            {
                temp = new Vector2(float.Parse(tempArry[0]), float.Parse(tempArry[1]));
            }

            return temp;
        }

        try
        {
            return System.Convert.ChangeType(value, type);
        }
        catch (Exception)
        {

            throw new Exception($"value:{value}  type:{type}");
        }
    }

    public static Type[] GetTypes<T>()
    {
        var ass = typeof(T).Assembly;
        var types = ass.GetTypes();
        List<Type> tempList = new List<Type>();
        for (int i = 0; i < types.Length; i++)
        {
            if (types[i].IsSubclassOf(typeof(T)))
            {
                tempList.Add(types[i]);
            }
        }

        return tempList.ToArray();
    }

    /// <summary>
    /// 判断字符能不能转成int类型
    /// </summary>
    /// <param name="value"></param>
    /// <param name="num"></param>
    /// <returns></returns>
    public static bool IsNumber(string value, out int num)
    {
        bool result = int.TryParse(value, out num);
        return result;
    }

    /// <summary>
    /// 获取打乱顺序后的全量随机列表（Fisher-Yates 洗牌算法，O(n) 复杂度，无反复 RemoveAt 搬移开销）
    /// </summary>
    /// <typeparam name="T">列表元素类型</typeparam>
    /// <param name="originList">源列表</param>
    /// <returns>随机排列后的新列表</returns>
    public static List<T> GetRandomList<T>(List<T> originList)
    {
        if (originList == null) return new List<T>();
        return GetRandomList(originList, originList.Count);
    }

    /// <summary>
    /// 从源列表中随机抽取指定数量的不重复元素（Fisher-Yates 局部洗牌）
    /// </summary>
    /// <typeparam name="T">列表元素类型</typeparam>
    /// <param name="originList">源列表</param>
    /// <param name="count">抽取的元素数量</param>
    /// <returns>随机抽取的结果列表</returns>
    public static List<T> GetRandomList<T>(List<T> originList, int count)
    {
        if (originList == null || count <= 0) return new List<T>();
        count = Mathf.Min(count, originList.Count);

        var tempList = new List<T>(originList);
        var randomList = new List<T>(count);

        for (int i = 0; i < count; i++)
        {
            int index = UnityEngine.Random.Range(i, tempList.Count);
            // 交换到头部以达成无搬移抽取
            T temp = tempList[i];
            tempList[i] = tempList[index];
            tempList[index] = temp;

            randomList.Add(tempList[i]);
        }

        return randomList;
    }

    /// <summary>
    /// 使用指定的伪随机数生成器，从源列表中随机抽取指定数量的不重复元素（支持指定随机种子）
    /// </summary>
    /// <typeparam name="T">列表元素类型</typeparam>
    /// <param name="originList">源列表</param>
    /// <param name="random">System.Random 实例</param>
    /// <param name="count">抽取的元素数量</param>
    /// <returns>随机抽取的结果列表</returns>
    public static List<T> GetRandomList<T>(List<T> originList, System.Random random, int count)
    {
        if (originList == null || count <= 0) return new List<T>();
        if (random == null) return GetRandomList(originList, count);

        count = Mathf.Min(count, originList.Count);
        var tempList = new List<T>(originList);
        var randomList = new List<T>(count);

        for (int i = 0; i < count; i++)
        {
            int index = random.Next(i, tempList.Count);
            T temp = tempList[i];
            tempList[i] = tempList[index];
            tempList[index] = temp;

            randomList.Add(tempList[i]);
        }

        return randomList;
    }

    public static void RunLater(MonoBehaviour mono, float waitTime, Action callback)
    {
        mono.StartCoroutine(Excute(waitTime, callback));
    }

    static IEnumerator Excute(float waitTime, Action callback)
    {
        yield return new WaitForSeconds(waitTime);
        callback.Invoke();
    }

    /// <summary>
    /// 判断层是否在所选层级中
    /// </summary>
    /// <param name="layer"></param>
    /// <param name="layerMask"></param>
    /// <returns></returns>
    public static bool IsSubLayer(int layer, LayerMask layerMask)
    {
        LayerMask mask = 1 << layer;
        return (mask & layerMask) == layerMask;
    }

    public static float TimeMsToS(int timeMs)
    {
        return timeMs * 0.001f;
    }

    public static T CreateClassByTypeName<T>(string typeName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var nameSpace = MethodBase.GetCurrentMethod().DeclaringType.Namespace;
        T entity = (T)assembly.CreateInstance($"{nameSpace}.{typeName}");
        if (entity == null)
        {
            throw new Exception("未设置的类型:" + typeName);
        }

        return entity;
    }
    public static string FormatTimeStr(float seconds)
    {
        // 确保时间不为负
        seconds = Mathf.Max(0, seconds);

        // 计算分钟和秒数
        int minutes = Mathf.FloorToInt(seconds / 60);
        int remainingSeconds = Mathf.FloorToInt(seconds % 60);

        // 格式化为两位数
        return string.Format("{0:00}:{1:00}", minutes, remainingSeconds);
    }
    #region 权重随机

    /// <summary>
    /// 获取随机Id列表
    /// </summary>
    /// <param name="strList"></param>
    /// <param name="count">数量</param>
    /// <param name="repetition">可重复</param>
    /// <returns></returns>
    public static List<T> GetWeightEnumTypeList<T>(System.Random random, List<string> strList, int count, bool repetition) where T : System.Enum
    {
        var idWeightDict = StringListTransitionWeightIntDict(strList);
        List<T> idList = new List<T>(count);
        for (int i = 0; i < count; i++)
        {
            var id = GetWeightId(random, idWeightDict);
            if (!repetition)
            {
                idWeightDict.Remove(id);
            }
            var enumValue = (T)Enum.Parse(typeof(T), id.ToString());
            idList.Add(enumValue);
        }
        return idList;
    }

    /// <summary>
    /// 获取随机Id列表
    /// </summary>
    /// <param name="strList"></param>
    /// <param name="count">数量</param>
    /// <param name="repetition">可重复</param>
    /// <returns></returns>
    public static List<int> GetWeightIdList(System.Random random, List<string> strList, int count, bool repetition)
    {
        var idWeightDict = StringListTransitionWeightIntDict(strList);
        return GetWeightIdList(random, idWeightDict, count, repetition);
    }
    /// <summary>
    /// 获取随机Id列表
    /// </summary>
    /// <param name="strList"></param>
    /// <param name="count">数量</param>
    /// <param name="repetition">可重复</param>
    /// <returns></returns>
    public static List<int> GetWeightIdList(System.Random random, Dictionary<int, int> idWeightDict, int count, bool repetition)
    {
        List<int> idList = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            var id = GetWeightId(random, idWeightDict);
            if (!repetition && idWeightDict.Count > 1)
            {
                idWeightDict.Remove(id);
            }
            idList.Add(id);
        }
        return idList;
    }
    public static int GetWeightId(System.Random random, Dictionary<int, int> idWeightDict)
    {
        int weightSum = 0;

        foreach (var keyPair in idWeightDict)
        {
            weightSum += keyPair.Value;
        }

        float value = random.Next(0, weightSum);

        float currentWeight = 0;
        foreach (var keyPair in idWeightDict)
        {
            currentWeight += keyPair.Value;
            if (value <= currentWeight)
            {
                return keyPair.Key;
            }
        }


        Debug.LogError("[Util] 没有随机到任何值：权重和为" + weightSum);
        return 0;
    }



    /// <summary>
    /// 获取随机Id列表
    /// </summary>
    /// <param name="strList"></param>
    /// <param name="count">数量</param>
    /// <param name="repetition">可重复</param>
    /// <returns></returns>
    public static List<int> GetWeightIdList(List<string> strList, int count, bool repetition)
    {
        var idWeightDict = StringListTransitionWeightFloatDict(strList);
        List<int> idList = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            var id = GetWeightId(idWeightDict);
            if (!repetition)
            {
                idWeightDict.Remove(id);
            }
            idList.Add(id);
        }
        return idList;
    }
    public static Dictionary<int, int> StringListTransitionWeightIntDict(List<string> strList)
    {
        Dictionary<int, int> idWeightDict = new Dictionary<int, int>();
        foreach (var str in strList)
        {
            var tmpArray = str.Split(':');
            if (tmpArray.Length >= 2)
            {
                if (!int.TryParse(tmpArray[0], out int id))
                {
                    Debug.LogError($"[Util] id转换错误:{str}");
                    continue;
                }
                if (!int.TryParse(tmpArray[1], out int weight))
                {
                    Debug.LogError($"[Util] weight转换错误:{str}");
                    continue;
                }
                if (idWeightDict.ContainsKey(id))
                {
                    Debug.LogError($"[Util] 权重id重复:{str}");
                    continue;
                }
                idWeightDict.Add(id, weight);
            }
        }
        return idWeightDict;
    }

    public static Dictionary<int, float> StringListTransitionWeightFloatDict(List<string> strList)
    {
        Dictionary<int, float> idWeightDict = new Dictionary<int, float>();
        foreach (var str in strList)
        {
            var tmpArray = str.Split(':');
            if (tmpArray.Length >= 2)
            {
                if (!int.TryParse(tmpArray[0], out int id))
                {
                    Debug.LogError($"[Util] id转换错误:{str}");
                    continue;
                }
                if (!float.TryParse(tmpArray[1], out float weight))
                {
                    Debug.LogError($"[Util] weight转换错误:{str}");
                    continue;
                }
                if (idWeightDict.ContainsKey(id))
                {
                    Debug.LogError($"[Util] 权重id重复:{str}");
                    continue;
                }
                idWeightDict.Add(id, weight);
            }
        }
        return idWeightDict;
    }
    public static int GetWeightId(List<string> strList)
    {
        var idWeightDict = StringListTransitionWeightFloatDict(strList);
        return GetWeightId(idWeightDict);
    }

    public static int GetWeightId(Dictionary<int, float> idWeightDict)
    {
        float weightSum = 0;

        foreach (var keyPair in idWeightDict)
        {
            weightSum += keyPair.Value;
        }

        float value = UnityEngine.Random.Range(0, weightSum);

        float currentWeight = 0;
        foreach (var keyPair in idWeightDict)
        {
            currentWeight += keyPair.Value;
            if (value <= currentWeight)
            {
                return keyPair.Key;
            }
        }


        Debug.LogError("[Util] 没有随机到任何值：权重和为" + weightSum);
        return 0;
    }

    public static int GetWeightId(Dictionary<int, int> idWeightDict)
    {
        float weightSum = 0;

        foreach (var keyPair in idWeightDict)
        {
            weightSum += keyPair.Value;
        }

        float value = UnityEngine.Random.Range(0, weightSum);

        float currentWeight = 0;
        foreach (var keyPair in idWeightDict)
        {
            currentWeight += keyPair.Value;
            if (value <= currentWeight)
            {
                return keyPair.Key;
            }
        }


        Debug.LogError("[Util] 没有随机到任何值：权重和为" + weightSum);
        return 0;
    }

    public static T GetWeight<T>(Dictionary<T, int> idWeightDict)
    {
        float weightSum = 0;

        foreach (var keyPair in idWeightDict)
        {
            weightSum += keyPair.Value;
        }

        float value = UnityEngine.Random.Range(0, weightSum);

        float currentWeight = 0;
        foreach (var keyPair in idWeightDict)
        {
            currentWeight += keyPair.Value;
            if (value <= currentWeight)
            {
                return keyPair.Key;
            }
        }


        Debug.LogError("[Util] 没有随机到任何值：权重和为" + weightSum);
        return default(T);
    }



    public static int GetWeightId(Dictionary<int, int> idWeightDict, System.Random random)
    {
        if (idWeightDict.Count == 0)
            return 0;
        int weightSum = 0;

        foreach (var keyPair in idWeightDict)
        {
            weightSum += keyPair.Value;
        }

        int value = random.Next(0, weightSum);

        float currentWeight = 0;
        foreach (var keyPair in idWeightDict)
        {
            currentWeight += keyPair.Value;
            if (value <= currentWeight)
            {
                return keyPair.Key;
            }
        }

        Debug.LogError("[Util] 没有随机到任何值：权重和为" + weightSum);
        return 0;
    }

    public static int GetWeightId(Dictionary<int, int> idWeightDict, int randomValue)
    {
        float currentWeight = 0;
        foreach (var keyPair in idWeightDict)
        {
            currentWeight += keyPair.Value;
            if (randomValue <= currentWeight)
            {
                return keyPair.Key;
            }
        }

        Debug.LogError("[Util] 没有随机到任何值");
        return 0;
    }
    #endregion
}