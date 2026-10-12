using System;
using System.Collections;
using System.Collections.Generic;
using JarVis.Warehouse.App;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Data;
using JarVis.Warehouse.Localization;
using JarVis.Warehouse.Providers;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse
{
    public sealed class WarehouseTaskController : MonoBehaviour
    {
        [SerializeField] private Text shiftLabel;
        [SerializeField] private Text taskTitleLabel;
        [SerializeField] private Text taskDetailLabel;
        [SerializeField] private Text taskStateLabel;
        [SerializeField] private Text progressLabel;
        [SerializeField] private Text activityLabel;
        [SerializeField] private Image progressFill;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button scanButton;
        [SerializeField] private Button completeButton;
        [SerializeField] private Button nextButton;

        private MockWarehouseRepository repository;
        private IReadOnlyList<WarehouseTask> tasks;
        private int currentTaskIndex;
        private bool usingServerTasks;
        private bool taskActionPending;

        private void Awake()
        {
            repository = new MockWarehouseRepository();
            usingServerTasks = OmsMobileSession.IsReady;
            tasks = usingServerTasks
                ? new List<WarehouseTask>()
                : repository.GetAssignedTasks(JarvisAppSession.EmployeeId);
            AddListeners();
            JarvisLocalization.LanguageChanged += OnLanguageChanged;
            RefreshLocalization();
            if (usingServerTasks)
            {
                Refresh(Local("Đang tải nhiệm vụ từ web quản lý...", "Loading tasks from the manager web..."));
                StartCoroutine(LoadServerTasks());
            }
            else
                Refresh(JarvisLocalization.Get("mock_data_ready"));
        }

        private void OnDestroy()
        {
            RemoveListeners();
            JarvisLocalization.LanguageChanged -= OnLanguageChanged;
        }

        private void ShowPreviousTask()
        {
            if (!HasTasks())
                return;

            currentTaskIndex = (currentTaskIndex - 1 + tasks.Count) % tasks.Count;
            Refresh(JarvisLocalization.Get("viewing_previous"));
        }

        private void ShowNextTask()
        {
            if (!HasTasks())
                return;

            currentTaskIndex = (currentTaskIndex + 1) % tasks.Count;
            Refresh(JarvisLocalization.Get("viewing_next"));
        }

        private void SimulateScan()
        {
            if (!HasTasks())
                return;
            if (usingServerTasks)
            {
                if (!taskActionPending)
                    StartCoroutine(StartAndLookupServerTask());
                return;
            }

            WarehouseTask task = tasks[currentTaskIndex];
            WarehouseItem item = repository.FindByBarcode(task.barcode);
            if (task.status == WarehouseTaskStatus.Assigned)
                task.status = WarehouseTaskStatus.InProgress;

            string result = item == null
                ? JarvisLocalization.Get("barcode_not_found")
                : $"{JarvisLocalization.Get("mock_match")} {item.sku} @ {item.location}. "
                    + JarvisLocalization.Get("inventory_unverified");
            Refresh(result);
        }

        private void CompleteCurrentTask()
        {
            if (!HasTasks())
                return;
            if (usingServerTasks)
            {
                if (!taskActionPending)
                    StartCoroutine(CompleteServerTask());
                return;
            }

            WarehouseTask task = tasks[currentTaskIndex];
            task.status = WarehouseTaskStatus.Completed;
            int completedCount = GetCompletedCount();
            if (completedCount < tasks.Count)
                MoveToNextIncompleteTask();

            Refresh($"MOCK UPDATE — {task.taskId}: {JarvisLocalization.Get("completed")}");
        }

        private IEnumerator LoadServerTasks()
        {
            OmsMobileApi.TaskList result = null;
            string error = null;
            yield return OmsMobileApi.GetTasks(value => result = value, value => error = value);
            if (result == null)
            {
                Refresh(Local("Không tải được nhiệm vụ: ", "Unable to load tasks: ") + error);
                yield break;
            }
            List<WarehouseTask> mapped = new List<WarehouseTask>();
            foreach (OmsMobileApi.TaskRow row in result.tasks ?? Array.Empty<OmsMobileApi.TaskRow>())
            {
                mapped.Add(new WarehouseTask
                {
                    taskId = row.id,
                    taskCode = row.task_code,
                    type = row.task_type == "RECEIVING" ? WarehouseTaskType.Receiving
                        : row.task_type == "COUNTING" ? WarehouseTaskType.CycleCount
                        : WarehouseTaskType.Picking,
                    sku = row.sku ?? string.Empty,
                    productName = row.title ?? row.productName ?? string.Empty,
                    barcode = row.barcode ?? string.Empty,
                    requiredQuantity = row.expected_quantity,
                    actualQuantity = 0,
                    location = row.locationCode ?? string.Empty,
                    priority = row.priority == "HIGH" ? WarehouseTaskPriority.High : WarehouseTaskPriority.Normal,
                    status = row.status == "COMPLETED" ? WarehouseTaskStatus.Completed
                        : row.status == "IN_PROGRESS" ? WarehouseTaskStatus.InProgress
                        : row.status == "ACCEPTED" ? WarehouseTaskStatus.Accepted
                        : row.status == "BLOCKED" ? WarehouseTaskStatus.NeedsReview
                        : WarehouseTaskStatus.Assigned,
                    language = "vi",
                    isMock = result.synthetic
                });
            }
            tasks = mapped;
            currentTaskIndex = Mathf.Clamp(currentTaskIndex, 0, Mathf.Max(0, mapped.Count - 1));
            Refresh(mapped.Count == 0
                ? Local("Chưa có nhiệm vụ được giao.", "No tasks are assigned.")
                : Local("Nhiệm vụ đã đồng bộ từ web quản lý (dữ liệu mẫu).",
                    "Tasks synced from manager web (sample data)."));
        }

        private IEnumerator StartAndLookupServerTask()
        {
            taskActionPending = true;
            WarehouseTask task = tasks[currentTaskIndex];
            if (task.status == WarehouseTaskStatus.Assigned)
            {
                OmsMobileApi.TaskRow accepted = null;
                string error = null;
                yield return OmsMobileApi.ChangeTaskStatus(task.taskId, "ACCEPTED",
                    value => accepted = value, value => error = value);
                if (accepted == null)
                {
                    Refresh(Local("Không nhận được nhiệm vụ: ", "Unable to accept task: ") + error);
                    taskActionPending = false;
                    yield break;
                }
                task.status = WarehouseTaskStatus.Accepted;
            }
            if (task.status == WarehouseTaskStatus.Accepted)
            {
                OmsMobileApi.TaskRow started = null;
                string error = null;
                yield return OmsMobileApi.ChangeTaskStatus(task.taskId, "IN_PROGRESS",
                    value => started = value, value => error = value);
                if (started == null)
                {
                    Refresh(Local("Đã nhận việc nhưng chưa bắt đầu: ", "Accepted but could not start: ") + error);
                    taskActionPending = false;
                    yield break;
                }
                task.status = WarehouseTaskStatus.InProgress;
            }
            if (string.IsNullOrWhiteSpace(task.barcode))
            {
                Refresh(Local("Đã bắt đầu. Nhiệm vụ này không có barcode sản phẩm.",
                    "Started. This task has no product barcode."));
                taskActionPending = false;
                yield break;
            }
            OmsMobileApi.InventoryList inventory = null;
            string lookupError = null;
            yield return OmsMobileApi.SearchInventory(task.barcode,
                value => inventory = value, value => lookupError = value);
            int total = 0;
            foreach (OmsMobileApi.InventoryRow row in inventory?.items ?? Array.Empty<OmsMobileApi.InventoryRow>())
                total += row.available;
            Refresh(inventory == null
                ? Local("Tra tồn kho thất bại: ", "Inventory lookup failed: ") + lookupError
                : $"{task.barcode}: {total} {Local("khả dụng", "available")} — "
                    + Local("dữ liệu mẫu từ backend; camera chưa giải mã barcode.",
                        "sample backend data; camera does not decode barcodes yet."));
            taskActionPending = false;
        }

        private IEnumerator CompleteServerTask()
        {
            taskActionPending = true;
            WarehouseTask task = tasks[currentTaskIndex];
            if (task.status != WarehouseTaskStatus.InProgress)
            {
                Refresh(Local("Hãy bắt đầu nhiệm vụ trước khi hoàn tất.",
                    "Start the task before completing it."));
                taskActionPending = false;
                yield break;
            }
            OmsMobileApi.TaskRow completed = null;
            string error = null;
            yield return OmsMobileApi.ChangeTaskStatus(task.taskId, "COMPLETED",
                value => completed = value, value => error = value);
            taskActionPending = false;
            if (completed == null)
            {
                Refresh(Local("Không cập nhật được web: ", "Could not update the web: ") + error);
                yield break;
            }
            task.status = WarehouseTaskStatus.Completed;
            Refresh(Local("Đã hoàn tất; web quản lý đã nhận trạng thái. Tồn kho không tự đổi.",
                "Completed; manager web has the status. Inventory was not changed."));
            yield return LoadServerTasks();
        }

        private void MoveToNextIncompleteTask()
        {
            for (int offset = 1; offset <= tasks.Count; offset++)
            {
                int candidateIndex = (currentTaskIndex + offset) % tasks.Count;
                if (tasks[candidateIndex].status != WarehouseTaskStatus.Completed)
                {
                    currentTaskIndex = candidateIndex;
                    return;
                }
            }
        }

        private void Refresh(string activity)
        {
            RefreshHeader();
            if (activityLabel != null)
                activityLabel.text = activity;
            if (!HasTasks())
            {
                if (taskTitleLabel != null)
                    taskTitleLabel.text = usingServerTasks
                        ? Local("Hàng nhiệm vụ", "Task queue")
                        : JarvisLocalization.Get("tasks");
                if (taskDetailLabel != null)
                    taskDetailLabel.text = activity;
                if (progressLabel != null)
                    progressLabel.text = "0 / 0";
                if (progressFill != null)
                    progressFill.fillAmount = 0f;
                if (completeButton != null)
                    completeButton.interactable = false;
                return;
            }

            WarehouseTask task = tasks[currentTaskIndex];
            int completedCount = GetCompletedCount();
            int highPriorityCount = GetHighPriorityCount();

            if (taskTitleLabel != null)
            {
                string priorityPrefix = task.priority == WarehouseTaskPriority.High ? "[HIGH] " : string.Empty;
                taskTitleLabel.text = $"{priorityPrefix}{task.productName}";
            }

            if (taskDetailLabel != null)
            {
                taskDetailLabel.text =
                    $"{(string.IsNullOrWhiteSpace(task.taskCode) ? task.taskId : task.taskCode)}\nSKU: {task.sku}  |  {task.barcode}\n"
                    + $"{JarvisLocalization.Get("required")}: {task.requiredQuantity}  |  "
                    + $"{JarvisLocalization.Get("actual")}: {task.actualQuantity}\n"
                    + $"{JarvisLocalization.Get("location")}: {task.location}\n"
                    + (usingServerTasks
                        ? Local("DỮ LIỆU MẪU ĐỒNG BỘ BACKEND", "SAMPLE DATA SYNCED FROM BACKEND")
                        : JarvisLocalization.Get("mock_unverified"));
            }

            if (taskStateLabel != null)
            {
                taskStateLabel.text = task.status == WarehouseTaskStatus.Accepted
                    ? Local("ĐÃ NHẬN", "ACCEPTED")
                    : JarvisLocalization.Get(GetStatusKey(task.status)).ToUpperInvariant();
                taskStateLabel.color = GetStatusColor(task.status);
            }

            if (progressLabel != null)
            {
                progressLabel.text =
                    $"{completedCount} / {tasks.Count} {JarvisLocalization.Get("tasks_complete")}"
                    + $"  •  {highPriorityCount} {JarvisLocalization.Get("high_priority")}";
            }

            if (progressFill != null)
                progressFill.fillAmount = tasks.Count == 0 ? 0f : (float)completedCount / tasks.Count;
            if (completeButton != null)
                completeButton.interactable = usingServerTasks
                    ? task.status == WarehouseTaskStatus.InProgress
                    : task.status != WarehouseTaskStatus.Completed;
        }

        private void RefreshHeader()
        {
            if (shiftLabel == null)
                return;

            string roleKey = JarvisAppSession.Role.ToString().ToLowerInvariant();
            shiftLabel.text =
                $"{JarvisLocalization.Get("welcome")}, {JarvisAppSession.EmployeeDisplayName}"
                + $"  |  {JarvisLocalization.Get(roleKey)}  |  {DateTime.Now:HH:mm}";
        }

        private void RefreshLocalization()
        {
            SetButtonLabel(previousButton, JarvisLocalization.Get("previous"));
            SetButtonLabel(scanButton, usingServerTasks
                ? Local("Bắt đầu / tra mã", "Start / look up code")
                : JarvisLocalization.Get("mock_lookup"));
            SetButtonLabel(completeButton, JarvisLocalization.Get("complete"));
            SetButtonLabel(nextButton, JarvisLocalization.Get("next"));
        }

        private void OnLanguageChanged()
        {
            RefreshLocalization();
            Refresh(usingServerTasks
                ? Local("Dữ liệu mẫu đồng bộ backend", "Sample backend data")
                : JarvisLocalization.Get("mock_data_ready"));
        }

        private static string Local(string vi, string en)
        {
            return JarvisLocalization.CurrentLanguage == SupportedLanguage.Vi ? vi : en;
        }

        private bool HasTasks()
        {
            return tasks != null && tasks.Count > 0;
        }

        private int GetCompletedCount()
        {
            int completedCount = 0;
            foreach (WarehouseTask task in tasks)
            {
                if (task.status == WarehouseTaskStatus.Completed)
                    completedCount++;
            }

            return completedCount;
        }

        private int GetHighPriorityCount()
        {
            int highPriorityCount = 0;
            foreach (WarehouseTask task in tasks)
            {
                if (task.priority == WarehouseTaskPriority.High
                    && task.status != WarehouseTaskStatus.Completed)
                {
                    highPriorityCount++;
                }
            }

            return highPriorityCount;
        }

        private void AddListeners()
        {
            if (previousButton != null)
                previousButton.onClick.AddListener(ShowPreviousTask);
            if (scanButton != null)
                scanButton.onClick.AddListener(SimulateScan);
            if (completeButton != null)
                completeButton.onClick.AddListener(CompleteCurrentTask);
            if (nextButton != null)
                nextButton.onClick.AddListener(ShowNextTask);
        }

        private void RemoveListeners()
        {
            if (previousButton != null)
                previousButton.onClick.RemoveListener(ShowPreviousTask);
            if (scanButton != null)
                scanButton.onClick.RemoveListener(SimulateScan);
            if (completeButton != null)
                completeButton.onClick.RemoveListener(CompleteCurrentTask);
            if (nextButton != null)
                nextButton.onClick.RemoveListener(ShowNextTask);
        }

        private static string GetStatusKey(WarehouseTaskStatus status)
        {
            return status switch
            {
                WarehouseTaskStatus.Assigned => "assigned",
                WarehouseTaskStatus.InProgress => "in_progress",
                WarehouseTaskStatus.ShortPick => "short_pick",
                WarehouseTaskStatus.Damaged => "damaged",
                WarehouseTaskStatus.Completed => "completed",
                WarehouseTaskStatus.NeedsReview => "needs_review",
                _ => "assigned"
            };
        }

        private static Color GetStatusColor(WarehouseTaskStatus status)
        {
            return status switch
            {
                WarehouseTaskStatus.Completed => ColorFromHex("73E2A7"),
                WarehouseTaskStatus.ShortPick => ColorFromHex("F9B65D"),
                WarehouseTaskStatus.Damaged => ColorFromHex("FF6B6B"),
                WarehouseTaskStatus.NeedsReview => ColorFromHex("F9B65D"),
                _ => ColorFromHex("55D6F4")
            };
        }

        private static void SetButtonLabel(Button button, string value)
        {
            if (button == null)
                return;

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = value;
        }

        private static Color ColorFromHex(string hex)
        {
            return ColorUtility.TryParseHtmlString($"#{hex}", out Color color) ? color : Color.white;
        }
    }
}
