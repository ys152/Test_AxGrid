using System;
using System.Collections.Generic;
using AxGrid;
using AxGrid.Base;
using AxGrid.FSM;
using AxGrid.Model;
using AxGrid.Path;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class Main : MonoBehaviourExtBind
{
    private const float ITEM_SIZE = 200;
    private const float ITEM_OFFSET = 10;
    private const float ITEM_SIZE_WITH_OFFSET = ITEM_SIZE + ITEM_OFFSET;
    private static Main instance;
    [SerializeField] private ParticleSystem confetty;
    [SerializeField] private TextMeshProUGUI victoryText;
    [SerializeField] private Button startBtn;
    [SerializeField] private Button stopBtn;
    [SerializeField] private float maxSpeed = 500;
    [SerializeField] private RectTransform slotParent;
    [SerializeField] private Item[] itemDatabase = new Item[26];
    private readonly Slot[] slots = new Slot[5]; 
    [OnStart]
    private void StartThis()
    {
        instance = this;
        stopBtn.interactable = false;
        startBtn.interactable = true;
        for (var i = 0; i < slots.Length; i++)
        {
            var slot = CreateSlot();
            slot.transform.SetParent(slotParent);
            slot.image.sprite = itemDatabase[GetRandomItemID()].sprite;
            slot.transform.localScale = new float3(1f);
            slots[i] = slot;
        }
        UpdateSlotPositions(0);
        Model.Set("MaxSpeed", maxSpeed);
        Model.Set("Position", 0);
        Settings.Fsm = new FSM();
        Settings.Fsm.Add(new ExInitState());
        Settings.Fsm.Add(new ExReadyState());
        Settings.Fsm.Add(new StartSpinningState());
        Settings.Fsm.Add(new StoppingState());
        Settings.Fsm.Add(new RollbackState());
        Settings.Fsm.Start("Init");
    }

    [OnUpdate]
    private void UpdateThis()
    {
        Settings.Fsm.Update(Time.deltaTime);
    }

    private void MoveToCenterSlotPositions(float deltaTime)
    {
        for (var i = 0; i < slots.Length; i++)
        {
            
        }
    }

    private void UpdateSlotPositions(float deltaTime)
    {
        var speed = Settings.Model.GetFloat("Speed");
        var position = Settings.Model.GetFloat("Position");
            position -= speed * deltaTime;
            if (position <= -ITEM_SIZE_WITH_OFFSET/2)
            {
                var slot = slots[0];
                for (int i = 1; i < slots.Length; i++) slots[i - 1] = slots[i];
                slot.image.sprite = itemDatabase[GetRandomItemID()].sprite;
                slots[^1] = slot;
                position += ITEM_SIZE_WITH_OFFSET;
            }

            Model.Set("Position", position);

        ApplyPositions(position);
    }

    private void ApplyPositions(float position)
    {
        for (var i = 0; i < slots.Length; i++)
        {
            var pos = (i-2f)*ITEM_SIZE_WITH_OFFSET+position;
            slots[i].outline.effectColor = IsWinningPosition(pos) ? Color.black : Color.white;
            slots[i].transform.localPosition = new float3(0, pos, 0);
        }
    }

    private bool IsWinningPosition(float position)
    {
        return math.distance(position, 0) < ITEM_SIZE_WITH_OFFSET / 2;
    }

    private int GetWinningIndex()
    {
        for (var i = 0; i < slots.Length; i++)
            if (IsWinningPosition(slots[i].transform.localPosition.y))
                return i;
        return -1;
    }

    private int GetRandomItemID()
    {
        return Random.Range(0, itemDatabase.Length);
    }

    private static Slot CreateSlot()
    {
        Slot slot = default;
        slot.transform = new GameObject().transform;
        slot.outline = new GameObject("outline", typeof(RectTransform)).AddComponent<Outline>();
        slot.outline.effectDistance = new float2(2, 2);
        slot.outline.gameObject.AddComponent<Image>();
        slot.outline.transform.SetParent(slot.transform);
        slot.outline.GetComponent<RectTransform>().sizeDelta = new float2(ITEM_SIZE, ITEM_SIZE);
        slot.outline.GetComponent<Image>().color = Color.gray;
        slot.image = new GameObject("image", typeof(RectTransform)).AddComponent<Image>();
        slot.image.transform.SetParent(slot.transform);
        slot.image.GetComponent<RectTransform>().sizeDelta = new float2(ITEM_SIZE, ITEM_SIZE);
        return slot;
    }

    [Bind("OnStartClick")]
    private void BindEventOne(string[] args)
    {
        Settings.Fsm.Change("StartSpinning");
    }

    [Bind("OnStopClick")]
    private void BindEventTwo(string[] args)
    {
        Settings.Fsm.Change("StopSpinning");
    }

    private struct Slot
    {
        public Transform transform;
        public Image image;
        public Outline outline;
    }

    [Serializable]
    private struct Item
    {
        public Sprite sprite;
    }

    [State("Init")]
    public class ExInitState : FSMState
    {
        [Enter]
        private void EnterThis()
        {
            Parent.Change("Ready");
        }
    }

    [State("Ready")]
    public class ExReadyState : FSMState
    {
    }

    [State("StartSpinning")]
    public class StartSpinningState : FSMState
    {
        [Enter]
        private void EnterThis()
        {
            var time = 3f;
            instance.victoryText.text = "";
            instance.startBtn.interactable = false;
            instance.Path.EasingCircEaseIn(time, 0, Settings.Model.GetFloat("MaxSpeed"),
                    value => { Model.Set("Speed", value); }).EasingCircEaseIn(0.2f, 0,
                    Settings.Model.GetFloat("MaxSpeed"), value => { Model.Set("Speed", value); })
                .Action(() => { instance.stopBtn.interactable = true; });
        }

        [Loop(0.0166f)]
        private void LoopThis()
        {
            instance.UpdateSlotPositions(0.0166f);
        }
    }


    [State("StopSpinning")]
    public class StoppingState : FSMState
    {
        [Enter]
        private void EnterThis()
        {
            var time = 3f;
            instance.stopBtn.interactable = false;
            instance.Path.EasingCircEaseIn(time, Settings.Model.GetFloat("MaxSpeed"), 0, 
                    value => { Model.Set("Speed", value); })
                .Action(() =>
                {
                    Settings.Fsm.Change("Rollback");
                });
        }

        [Loop(0.0166f)]
        private void LoopThis()
        {
            instance.UpdateSlotPositions(0.0166f);
        }
    }
    [State("Rollback")]
    public class RollbackState : FSMState
    {
        [Enter]
        private void EnterThis()
        {
            instance.Path
                .Wait(0.5f)
                .EasingCircEaseIn(1f, Settings.Model.GetFloat("Position"), 0,
                    value =>
                    {
                        Model.Set("Speed", 0);
                        Model.Set("Position", value);
                        instance.ApplyPositions(value);
                    })
                .Action(() =>
                {
                    Settings.Fsm.Change("Ready");
                    instance.startBtn.interactable = true;
                    instance.victoryText.text =
                        $"You won {instance.slots[instance.GetWinningIndex()].image.sprite.name}!";
                    instance.confetty.Play();
                });
        }
    }
}