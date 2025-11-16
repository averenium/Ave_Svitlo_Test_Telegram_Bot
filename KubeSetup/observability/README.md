# Observability Stack для AVE Electricity Bot

Повний stack для моніторингу метрик вашого Telegram-бота на Kubernetes.

## 📊 Компоненти

- **Prometheus** - збір та зберігання метрик
- **OpenTelemetry Collector** - автоматичний scraping метрик з подів
- **Grafana** - візуалізація метрик з готовим дашбордом

## 🚀 Швидкий старт

### 1. Deploy Prometheus

```powershell
kubectl apply -f observability/prometheus/rbac.yaml
kubectl apply -f observability/prometheus/pvc.yaml        # Persistent storage
kubectl apply -f observability/prometheus/configmap.yaml
kubectl apply -f observability/prometheus/deployment.yaml
```

### 2. Deploy OpenTelemetry Collector

```powershell
kubectl apply -f observability/otel-collector/rbac.yaml
kubectl apply -f observability/otel-collector/configmap.yaml
kubectl apply -f observability/otel-collector/deployment.yaml
```

### 3. Deploy Grafana

```powershell
kubectl apply -f observability/grafana/pvc.yaml           # Persistent storage
kubectl apply -f observability/grafana/configmap.yaml
kubectl apply -f observability/grafana/deployment.yaml
```

### 4. Update вашого бота (вже оновлено)

Deployment вашого бота вже має необхідні анотації для автоматичного scraping:

```yaml
annotations:
  prometheus.io/scrape: "true"
  prometheus.io/port: "8080"
  prometheus.io/path: "/metrics"
```

### 5. Deploy/Re-deploy бота

```powershell
kubectl apply -f app/deployment.yaml
```

## 🔍 Як це працює

### Автоматичний Scraping

1. **Prometheus** і **OpenTelemetry Collector** автоматично знаходять всі поди з анотацією `prometheus.io/scrape: "true"`
2. Вони scrape метрики з endpoint'у вказаного в `prometheus.io/path` на порті `prometheus.io/port`
3. Метрики зберігаються в Prometheus
4. Grafana візуалізує їх через готовий дашборд

### Архітектура

```
┌─────────────────────────────────────┐
│  AVE Bot Pod                        │
│  - annotations:                     │
│    prometheus.io/scrape: "true"     │
│  - exposes /metrics on :8080        │
└─────────────────────────────────────┘
         │                    │
         │ scrape             │ scrape
         ↓                    ↓
┌─────────────────┐  ┌──────────────────────┐
│  Prometheus     │  │  OTel Collector      │
│  :9090          │←─│  (remote write)      │
└─────────────────┘  └──────────────────────┘
         │
         │ query
         ↓
┌─────────────────┐
│  Grafana        │
│  :3000          │
└─────────────────┘
```

## 🌐 Доступ до UI

### Grafana (NodePort)
```powershell
# Відкрити Grafana в браузері
# URL: http://localhost:30300
# або http://<node-ip>:30300
```

**Credentials:**
- Username: `admin`
- Password: `admin`

### Prometheus (Port Forward)
```powershell
kubectl port-forward svc/prometheus 9090:9090
# URL: http://localhost:9090
```

### OpenTelemetry Collector Metrics (Port Forward)
```powershell
kubectl port-forward svc/otel-collector 8889:8889
# URL: http://localhost:8889/metrics
```

## 📈 Готовий Дашборд

Grafana вже містить готовий дашборд "AVE Svitlo Bot - Metrics" з наступними панелями:

1. **HTTP Request Rate** - кількість запитів per second
2. **HTTP Request Duration P95** - 95-й перцентиль латентності
3. **Memory Usage** - використання пам'яті
4. **CPU Usage** - використання CPU

## 🔧 Налаштування ASP.NET Core додатку

Ваш ASP.NET Core додаток повинен експортувати метрики на `/metrics` endpoint. 

### Додайте до вашого проєкту:

**NuGet packages:**
```powershell
dotnet add package OpenTelemetry.Exporter.Prometheus.AspNetCore
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
dotnet add package OpenTelemetry.Instrumentation.Runtime
```

**Program.cs:**
```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

var builder = WebApplication.CreateBuilder(args);

// Додати OpenTelemetry Metrics
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("ave-svitlo-test-bot"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

var app = builder.Build();

// Expose /metrics endpoint
app.MapPrometheusScrapingEndpoint();

app.Run();
```

## 📝 Додавання власних метрик

```csharp
using System.Diagnostics.Metrics;

public class TelegramBotMetrics
{
    private readonly Counter<long> _messageCounter;
    private readonly Histogram<double> _messageProcessingTime;
    
    public TelegramBotMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("AVE.TelegramBot");
        
        _messageCounter = meter.CreateCounter<long>(
            "telegram_messages_total",
            description: "Total number of Telegram messages processed");
            
        _messageProcessingTime = meter.CreateHistogram<double>(
            "telegram_message_processing_seconds",
            description: "Time to process Telegram message");
    }
    
    public void RecordMessage(string messageType) 
        => _messageCounter.Add(1, new("message_type", messageType));
    
    public void RecordProcessingTime(double seconds) 
        => _messageProcessingTime.Record(seconds);
}

// Реєстрація
builder.Services.AddSingleton<TelegramBotMetrics>();
```

## 🔍 Перевірка роботи

### 1. Перевірити що поди запущені
```powershell
kubectl get pods -l app=prometheus
kubectl get pods -l app=otel-collector
kubectl get pods -l app=grafana
kubectl get pods -l app=ave-svitlo-test-bot
```

### 2. Перевірити що Prometheus scrape targets
```powershell
kubectl port-forward svc/prometheus 9090:9090
# Відкрити http://localhost:9090/targets
```

### 3. Перевірити метрики з бота
```powershell
kubectl port-forward svc/ave-svitlo-test-bot-service 9191:9191
# curl http://localhost:9191/metrics
```

### 4. Перевірити що OTel Collector scrape
```powershell
kubectl logs -l app=otel-collector -f
# Шукати "Scrape succeeded" в логах
```

## 📊 Корисні Prometheus queries

### HTTP Request Rate
```promql
rate(http_server_requests_total{app="ave-svitlo-test-bot"}[5m])
```

### HTTP Request Duration P95
```promql
histogram_quantile(0.95, rate(http_server_request_duration_seconds_bucket{app="ave-svitlo-test-bot"}[5m]))
```

### Memory Usage
```promql
process_working_set_bytes{app="ave-svitlo-test-bot"}
```

### CPU Usage
```promql
rate(process_cpu_seconds_total{app="ave-svitlo-test-bot"}[5m]) * 100
```

### Error Rate
```promql
rate(http_server_requests_total{app="ave-svitlo-test-bot",status=~"5.."}[5m])
```

## 🧹 Cleanup

```powershell
# Видалити всі компоненти observability
kubectl delete -f observability/grafana/
kubectl delete -f observability/otel-collector/
kubectl delete -f observability/prometheus/
```

## 💾 Persistent Storage

### Що використовується:

**Prometheus** (`prometheus-storage` PVC):
- 10Gi disk для зберігання метрик
- Retention: 15 днів (налаштовано в deployment)
- Дані зберігаються при рестартах

**Grafana** (`grafana-storage` PVC):
- 5Gi disk для дашбордів і налаштувань
- Зберігає користувацькі дашборди, datasources, users

### Якщо немає StorageClass:

**Варіант 1:** Використати emptyDir (тимчасове):
```yaml
- name: prometheus-storage
  emptyDir: {}  # Дані зникнуть при рестарті
```

**Варіант 2:** Створити local PersistentVolume:
```yaml
apiVersion: v1
kind: PersistentVolume
metadata:
  name: prometheus-pv
spec:
  capacity:
    storage: 10Gi
  accessModes:
    - ReadWriteOnce
  hostPath:
    path: /mnt/data/prometheus
  persistentVolumeReclaimPolicy: Retain
```

**Варіант 3:** Вказати StorageClass в pvc.yaml:
```yaml
storageClassName: standard  # або ваш StorageClass
```

### Перевірити доступні StorageClasses:
```powershell
kubectl get storageclass
```

## 🎯 Наступні кроки

1. **Додати Alerting** - налаштувати AlertManager для сповіщень
2. **Додати Traces** - додати distributed tracing через Jaeger/Tempo
3. **Додати Logs** - налаштувати Loki для централізованого логування
4. **Custom Dashboards** - створити більш деталізовані дашборди
5. **Backup Strategy** - налаштувати backup для Prometheus/Grafana data

## 📚 Ресурси

- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)
- [Prometheus](https://prometheus.io/docs/)
- [Grafana](https://grafana.com/docs/)
- [OTel Collector](https://opentelemetry.io/docs/collector/)
