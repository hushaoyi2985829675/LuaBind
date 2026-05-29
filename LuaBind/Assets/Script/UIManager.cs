// using Cinemachine;

using System;

using System.Collections;

using System.Collections.Generic;

// using DG.Tweening;

using Unity.VisualScripting;

using UnityEditor;

using UnityEngine;

using UnityEngine.SceneManagement;

using UnityEngine.UI;



// [Serializable]

// public class LayerInfo

// {

//     public string name;

//     public PanelBase layer;

//     public GameObject maskLayer;

//     public LayerInfo(string name, PanelBase layer, GameObject maskLayer)

//     {

//         this.name = name;

//         this.layer = layer;

//         this.maskLayer = maskLayer;

//     }

// }



// #if false

// public class UIManager : Singleton<UIManager>

// {

//     [Header("Layer遮罩")]

//     [SerializeField]

//     private GameObject maskLayerRef;

    

//     public List<LayerInfo> layerList;

//     private Stack<LayerInfo> layerStack;

//     private Dictionary<int, PanelBase> PopLayerList;

//     private Dictionary<int, int> instanceToPrefabGidList;

//     private Dictionary<int, GameObject> MapList;

//     private GameObject curMap;

//     private Transform layerCanvas;

//     private Transform popLayerCanvas;

//     private PanelBase curActPanelNode;

//     private void Awake()

//     {

//         layerList = new List<LayerInfo>();

//         layerStack =  new Stack<LayerInfo>();

//         MapList = new Dictionary<int, GameObject>();

//         PopLayerList = new Dictionary<int, PanelBase>();

//         instanceToPrefabGidList = new Dictionary<int, int>();

//         layerCanvas = GameObject.FindWithTag("LayerCanvas").transform;

//         popLayerCanvas = GameObject.FindWithTag("PopLayerCanvas").transform;

//         maskLayerRef = Ui.Instance.GetLayerPrefab("Common/MaskLayer");

//         {

//             GameObject layer = GameObject.Find("MainLayer");

//             LayerInfo layerInfo = new LayerInfo("MainLayer", layer.GetComponent<PanelBase>(), null);

//             layerList.Add(layerInfo);

//             layerStack.Push(layerInfo);

//         }

//     }



//     private void Start()

//     {

      

//     }

//     public LayerInfo AddLayer(GameObject layerRef, params object[] data)

//     {

//         LayerInfo newLayerInfo = GetLayer(layerRef.name);

//         PanelBase layerScript = newLayerInfo == null ?  null : newLayerInfo.layer ;

//         GameObject maskLayer = newLayerInfo == null ?  null : newLayerInfo.maskLayer ;

//         if (newLayerInfo == null)

//         {

//             if (layerRef == null)

//             {

//                 Debug.Log("界面预制体是空");

//                 return null;

//             }

//             GameObject layer = Instantiate(layerRef, layerCanvas);

//             layer.GetComponent<RectTransform>().anchoredPosition = Vector3.zero;

//             layer.name = layerRef.name;

//             layerScript = layer.GetComponent<PanelBase>();

//             if (layerScript == null)

//             {

//                 Debug.Log("页面脚本没有继承PanelBase");

//                 return null;

//             }

//             layerScript.onEnter(data);

//             if (!layerScript.isFullScreen)

//             {

//                 //加载背景遮罩

//                 maskLayer = Instantiate(maskLayerRef, layerCanvas);

//                 maskLayer.name = layerRef.name + "MaskLayer";

//                 maskLayer.GetComponent<MaskLayer>().SetPanel(layerScript);

//             }

//             newLayerInfo = new LayerInfo(layerRef.name, layerScript, maskLayer);

//             layerList.Add(newLayerInfo); 

//         }

        

//         if (!layerScript.isFullScreen)

//         {

//             maskLayer.SetActive(true);

//             maskLayer.transform.SetSiblingIndex(layerCanvas.childCount);

//             layerScript.transform.localScale = new Vector3(0.01f, 0.01f, 1);

//             layerScript.transform.DOScale(new Vector3(1, 1, 1), 0.3f).SetEase(Ease.OutCirc);

//             layerScript.transform.SetSiblingIndex(layerCanvas.childCount);

//         }

//         else

//         {

//             //压栈

//             layerStack.Push(newLayerInfo);

//         }

//         layerScript.SetActive(true);

//         layerScript.onShow(data);

//         //如果是全屏

//         if (layerScript.isFullScreen) 

//         {

//             int index = 0;

//             List<int> layerIdxList = new List<int>();

//             foreach (var layerInfo in layerList )

//             {

//                 PanelBase layer = layerInfo.layer;

//                 if (layerRef.name != layer.name)

//                 {

//                     //隐藏其他全屏并关闭弹窗

//                     CloseLayer(layer.name,false);

//                     if (!layer.isFullScreen)

//                     {

//                         layerIdxList.Add(index);

//                     }

//                 }

//                 index++;

//             }

//             for (int i = layerIdxList.Count - 1; i >= 0; i--)

//             {

//                 int idx = layerIdxList[i];

//                 LayerInfo layerInfo = layerList[idx];

//                 if (!layerInfo.layer.isFullScreen)

//                 {

//                     DOTween.Kill(layerInfo.layer.transform, false);

//                     Destroy(layerInfo.maskLayer.gameObject);

//                 }

//                 Destroy(layerInfo.layer.gameObject);

//                 layerList.RemoveAt(idx);

//             }

//             layerIdxList.Clear();

//         }

//         else

//         {

//             layerScript.transform.DOScale(new Vector3(1, 1, 1), 0.3f).SetEase(Ease.OutCirc);

//         }

        

//         return newLayerInfo;

//     }



//     public void CloseLayer(string layerName,bool isRestoreLastLayer = true)

//     {

//         LayerInfo layerInfo = GetLayer(layerName);

//         PanelBase layer = layerInfo.layer;

//         layer.Hide();

//         if (layer.isFullScreen)

//         {

//             layer.onExit();

//             layer.SetActive(false);

//             if (isRestoreLastLayer)

//             {

//                 EnableLastLayer();

//             }

//         }

//         else

//         {

//             GameObject maskLayer = layerInfo.maskLayer;

//             maskLayer.SetActive(false);

//             layer.transform.DOScale(new Vector3(0.01f, 0.01f, 1), 0.15f).SetEase(Ease.OutCirc).OnComplete(() =>

//             {

//                 layer.SetActive(false);

//                 layer.onExit();

//                 layer.transform.DOKill();

//                 layerStack.Pop();

//             });   

//         }

//     }

    

//     //清空栈的弹窗

//     // private void StackClearPop()

//     // {

//     //     LayerInfo layerInfo = layerStack.Peek();

//     //     if (!layerInfo.layer.IsFullScreen)

//     //     {

//     //         layerStack.Pop();

//     //         StackClearPop();

//     //     }

//     // }

//     //

//     //恢复上一个界面

//     private void EnableLastLayer()

//     {

//         //恢复上一个界面

//         layerStack.Pop();

//         LayerInfo lastLayerInfo = layerStack.Peek();

//         lastLayerInfo.layer.SetActive(true);

//         lastLayerInfo.layer.onShow();

//     }



//     public PanelBase AddUINode(GameObject layerRef, Transform parent, params object[] data)

//     {

//         var layer = Instantiate(layerRef, parent);

//         layer.GetComponent<RectTransform>().anchoredPosition = Vector3.zero;

//         layer.name = layerRef.name;

//         PanelBase layerScript = layer.GetComponent<PanelBase>();

//         layerScript.onEnter(data);

//         layerScript.transform.SetSiblingIndex(parent.childCount);

//         layerScript.onShow(data);

//         return layerScript;

//     }



//     public void ClosePopLayer(GameObject layer)

//     {

//         CloseUINode(layer);

//     }



//     public void CloseUINode(GameObject layer)

//     {

//         if (layer == null)

//         {

//             return;

//         }

//         PanelBase panelBase = layer.GetComponent<PanelBase>();

//         panelBase.transform.SetSiblingIndex(1);

//         panelBase.gameObject.SetActive(false);

//         panelBase.onExit();

//         panelBase.Hide();

//     }



//     private LayerInfo GetLayer(string layerName)

//     {

//         LayerInfo layerInfo = layerList.Find(x => x.name == layerName);

//         if (layerInfo == null)

//         {

//             return null;

//         }

//         return layerInfo;

//     }



//     private void DestroyLayer(PanelBase layer)

//     {

//         Destroy(layer.gameObject);

//     }



//     public Transform GetCanvas()

//     {

//         return GameObject.Find("InTurnCanvas").transform;

//     }



//     // public void LoadScene(string sceneName, int mapId)

//     // {

//     //     CameraManager.Instance.ChangeMapAction(() =>

//     //     {

//     //         OpenLayer(Resources.Load<LoadLayer>("Ref/LayerRef/UIRef/LoadLayer/LoadLayer").gameObject, new object[] {sceneName, mapId}).GetComponent<LoadLayer>();

//     //     });

//     //     

//     // }



//     // private void PlayAddAnimation(Transform trans)

//     // {

//     //     maskLayer.SetActive(true);

//     //     maskLayer.transform.SetSiblingIndex(layerCanvas.childCount);

//     //     trans.localScale = new Vector3(0.01f, 0.01f, 1);

//     //     trans.gameObject.SetActive(true);

//     //     trans.DOScale(new Vector3(1, 1, 1), 0.3f).SetEase(Ease.OutCirc);

//     // }



//     public void LoadMainScene()

//     {

//         //LoadScene("MainScene", (slider) => { });

//     }

// }

// #endif



// /// <summary>原实现已用 #if false 保留在文件上方；需要恢复时删掉本类并把 #if false 改为 #if true 或删除条件编译。</summary>

// public class UIManager : Singleton<UIManager>

// {

//     private void Awake() { }



//     private void Start() { }



//     public LayerInfo AddLayer(GameObject layerRef, params object[] data) => null;



//     public void CloseLayer(string layerName, bool isRestoreLastLayer = true) { }



//     public PanelBase AddUINode(GameObject layerRef, Transform parent, params object[] data) => null;



//     public void ClosePopLayer(GameObject layer) { }



//     public void CloseUINode(GameObject layer) { }



//     public Transform GetCanvas() => null;



//     public void LoadMainScene() { }

// }


