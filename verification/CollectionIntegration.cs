using System;
using System.Collections;
using System.IO;
using System.Linq;
using CalculateIt.FractionalNumbers;
using CalculateIt.ModLoader;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed partial class Integration
{
    private IEnumerator CheckCollection()
    {
        yield return new WaitForSecondsRealtime(8);
        Transform panel=null;
        Descriptionframe tooltip=null;
        Func<Infos[]> entries=()=>panel.GetComponentsInChildren<Infos>().Where(i=>i.name!=null && i.gameObject.name.StartsWith("Entry_")).ToArray();
        Step("mods page is a native notebook clone", () => {
            var canvas=GameObject.Find("Canvas (1)").transform;
            canvas.Find("ShakerItems/buttons/Mods").GetComponent<Button>().onClick.Invoke();
            panel=canvas.Find("ShakerItems/ModsMenu");
            var original=Resources.FindObjectsOfTypeAll<CollectionManage>().First(c=>c.gameObject.scene.name=="menu" && c.transform.parent!=panel);
            Assert(panel.localScale==original.transform.parent.localScale,"Native collection page scale changed");
            Assert(panel.Find("backload").GetComponent<Image>().sprite==original.transform.parent.Find("backload").GetComponent<Image>().sprite,"Native notebook sprite not reused");
            Assert(panel.Find("Paper")==null && panel.Find("ButtonsTab")==null,"Custom panel design remains");
            tooltip=canvas.Find("ShakerItems/ModDescription").GetComponent<Descriptionframe>();
            Assert(tooltip && panel.GetComponentsInChildren<Button>().Any(b=>b.name=="Mod_"+Plugin.Id),"Clickable native mod row absent");
        });
        yield return new WaitForSecondsRealtime(1);
        ScreenCapture.CaptureScreenshot(Path.Combine(result,"mods-native.png"));
        yield return new WaitForSecondsRealtime(.2f);
        Step("native six-column button collection", () => {
            panel.GetComponentsInChildren<Button>().Single(b=>b.name=="Mod_"+Plugin.Id).onClick.Invoke();
            var cards=entries(); Assert(cards.Length==45,"Button count incorrect");
            Assert(cards.All(i=>!i.enabled && i.can && i.description.Length>0),"Native tooltip metadata incomplete");
            var root=cards.Single(i=>i.gameObject.name=="Entry_operator_fraction_sqrt");
            Assert(root.GetComponent<Randomisator>() && root.GetComponent<Popup>(),"Native collection key and hover animation missing");
            ExecuteEvents.Execute(root.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
            Assert(tooltip.gg==root.gameObject && tooltip.ddesc.text.Contains("1.414214"),"Native tooltip does not show root description");
            ExecuteEvents.Execute(root.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
            var native=Resources.FindObjectsOfTypeAll<CollectionManage>().First(c=>c.gameObject.scene.name=="menu" && c.transform.parent!=panel);
            var first=cards[0].transform.localPosition; var seventh=cards[6].transform.localPosition;
            Assert(Math.Abs(first.x-seventh.x)<.01f && Math.Abs(first.y-seventh.y-native.spacingY)<.01f,"Grid differs from native six-column layout");
        });
        yield return new WaitForSecondsRealtime(1);
        ScreenCapture.CaptureScreenshot(Path.Combine(result,"collection-buttons.png"));
        yield return new WaitForSecondsRealtime(.2f);
        Step("native arrow switches to passive items", () => {
            panel.Find("right").GetComponent<Button>().onClick.Invoke();
            var cards=entries(); Assert(cards.Length==4 && cards.All(i=>i.passive),"Passive category missing");
            var lucky=cards.Single(i=>i.gameObject.name=="Entry_passive_fraction_lucky");
            ExecuteEvents.Execute(lucky.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
            Assert(tooltip.gg==lucky.gameObject && tooltip.ddesc.text.Contains("5"),"Native passive tooltip missing");
            ExecuteEvents.Execute(lucky.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
            Assert(panel.Find("left").gameObject.activeSelf && !panel.Find("right").gameObject.activeSelf,"Category arrow state wrong");
        });
        yield return new WaitForSecondsRealtime(1);
        ScreenCapture.CaptureScreenshot(Path.Combine(result,"collection-items.png"));
        yield return new WaitForSecondsRealtime(.2f);
        Step("pagination and native navigation", () => {
            var original=ModCollections.Get(Plugin.Id);
            var extra=Enumerable.Range(0,5).Select(i=>new CollectionEntry { Id="test_"+i, Kind=CollectionKind.Button, Symbol="T", RussianName="Тест",EnglishName="Test",RussianDescription="Описание",EnglishDescription="Description" });
            ModCollections.Register(Plugin.Id,original.Concat(extra));
            panel.Find("left").GetComponent<Button>().onClick.Invoke();
            Assert(entries().Length==48,"First page should have native 48-key capacity");
            panel.Find("right").GetComponent<Button>().onClick.Invoke();
            Assert(entries().Length==2 && entries().All(i=>!i.passive),"Second page missing");
            panel.Find("right").GetComponent<Button>().onClick.Invoke(); Assert(entries().Length==4,"Pagination did not reach passive collection");
            ModCollections.Register(Plugin.Id,original);
            panel.Find("Back").GetComponent<Button>().onClick.Invoke();
            panel.GetComponentsInChildren<Button>().Single(b=>b.name=="Mod_calculateit.fractions.verification").onClick.Invoke();
            Assert(panel.GetComponentsInChildren<Transform>().Any(t=>t.name=="Empty"),"Unregistered collection empty state missing");
            panel.Find("Back").GetComponent<Button>().onClick.Invoke(); panel.Find("Back").GetComponent<Button>().onClick.Invoke();
            Assert(!panel.gameObject.activeSelf && !tooltip.gameObject.activeSelf,"Back did not close custom pages and tooltip");
            var collectionButton=GameObject.Find("Canvas (1)").transform.Find("ShakerItems/buttons/Collection").GetComponent<Button>();
            collectionButton.onClick.Invoke();
            Assert(Resources.FindObjectsOfTypeAll<CollectionManage>().Any(c=>c.gameObject.activeInHierarchy && c.transform.parent!=panel),"Original collection navigation broken");
        });
        UnityEngine.Object.FindFirstObjectByType<LanguageManager>().SetLang("eng");
        yield return new WaitForSecondsRealtime(3);
        Step("English native collection after reload", () => {
            var canvas=GameObject.Find("Canvas (1)").transform; canvas.Find("ShakerItems/buttons/Mods").GetComponent<Button>().onClick.Invoke();
            panel=canvas.Find("ShakerItems/ModsMenu"); panel.GetComponentsInChildren<Button>().Single(b=>b.name=="Mod_"+Plugin.Id).onClick.Invoke();
            Assert(entries().Single(i=>i.gameObject.name=="Entry_operator_fraction_sqrt").name=="Fractional root","English button description wrong");
            panel.Find("right").GetComponent<Button>().onClick.Invoke();
            Assert(entries().Single(i=>i.gameObject.name=="Entry_passive_fraction_reserve").name=="Precision reserve","English passive description wrong");
        });
        File.AppendAllText(Path.Combine(result,"result.txt"),failed?"FAILED\n":"ALL PASS\n"); Application.Quit();
    }
}
