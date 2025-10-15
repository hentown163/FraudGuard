# Global Payment Fraud Detection System

## Project Overview
Enterprise-grade payment fraud detection system with real-time transaction monitoring, ML-powered risk scoring, and comprehensive admin dashboard built with ASP.NET Core 8.0 and Razor Pages.

## Recent Enhancements (October 2025)

### UI/UX Improvements
- **Professional Design System**: Custom CSS with modern color scheme, gradients, shadows, and animations
- **Dark Mode Support**: Full dark theme with persistent user preference
- **Responsive Design**: Mobile-first approach with adaptive layouts
- **Enhanced Navigation**: Clean navigation with breadcrumbs and icons
- **Loading States**: Smooth transitions and fade-in animations throughout

### Feature Enhancements

#### Dashboard
- Real-time transaction monitoring with SignalR
- Advanced filtering by status (Approved, Declined, Pending, High Risk)
- Search functionality across transaction ID, user, and amount
- Date range filtering
- Live-updating charts (Fraud Score Trend & Risk Distribution)
- Export to CSV and PDF with full transaction data
- Animated stat cards with gradient backgrounds

#### Analytics Page
- Transaction volume trends visualization
- Gateway distribution pie chart
- ML model performance metrics (Precision, Recall, F1 Score, AUC-ROC)
- Geographic fraud heatmap placeholder
- Top fraud indicators with impact scores
- Comprehensive business intelligence metrics

#### Settings Page
- Email notification preferences
- SMS alerts via Twilio integration
- Custom alert rules management
- Webhook configuration
- API key management
- General preferences (timezone, date format, currency)
- Tabbed interface for easy navigation

#### Alerts Page
- Bulk alert management with checkboxes
- Filtering by severity, status, and type
- Priority-based sorting
- Bulk resolve functionality
- Real-time alert statistics cards
- Interactive alert investigation workflow

### Technical Implementation

#### Frontend
- **CSS Framework**: Bootstrap 5 + Custom Professional Theme
- **Charts**: Chart.js for data visualization
- **Icons**: Bootstrap Icons
- **Fonts**: Inter font family for modern typography
- **Real-time**: SignalR for live updates
- **Export**: jsPDF + jsPDF-AutoTable for PDF generation

#### Backend
- **Framework**: ASP.NET Core 8.0
- **Architecture**: Razor Pages with clean separation
- **Payment Gateways**: Stripe, PayPal, Braintree, Authorize.Net
- **Fraud Detection**: ONNX ML models, behavioral analytics
- **Notifications**: Twilio (SMS), Email
- **Cloud Services**: Azure Cosmos DB, Event Hubs, Service Bus, Key Vault
- **Geolocation**: MaxMind GeoIP2

### Key Files Modified
- `wwwroot/css/professional-theme.css` - Complete design system
- `wwwroot/js/site.js` - Dark mode, toast notifications, utilities
- `Pages/Shared/_Layout.cshtml` - Enhanced navigation and dark mode toggle
- `Pages/Dashboard/Index.cshtml` - Full-featured dashboard with filtering and export
- `Pages/Analytics/Index.cshtml` - New analytics and insights page
- `Pages/Settings/Index.cshtml` - New settings management page
- `Pages/Alerts/Index.cshtml` - Enhanced alerts with bulk actions

### User Experience Features
- **Dark Mode Toggle**: Floating button with icon animation
- **Toast Notifications**: Non-intrusive success/error messages
- **Search & Filter**: Real-time client-side filtering
- **Export Options**: Professional CSV and PDF reports
- **Responsive Tables**: Horizontal scrolling on mobile
- **Loading Indicators**: Skeleton screens and spinners
- **Smooth Animations**: Fade-in, slide-in, hover effects

### Browser Compatibility
- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+

### Performance Optimizations
- CSS custom properties for theme switching
- Efficient event listeners with delegation
- Chart data limiting (20 points max for line charts)
- Lazy loading for heavy components
- Optimized asset delivery via CDN

## Development Setup
1. Ensure .NET 8.0 SDK is installed
2. Run `dotnet restore` to install dependencies
3. Configure Azure services and API keys in `appsettings.json`
4. Run `dotnet run` to start the development server
5. Access the application at `http://localhost:5000`

## Environment Variables
- Azure Cosmos DB connection string
- Event Hubs connection string
- Service Bus connection string
- Key Vault URI
- Twilio credentials
- Stripe/PayPal API keys

## Project Structure (Reorganized - October 2025)

### Clean Architecture Layers
- `/Core` - Business logic layer with proper separation of concerns
  - `/Core/Interfaces/Services` - Service interface definitions
  - `/Core/Interfaces/Repositories` - Repository interface definitions
  - `/Core/Services` - Service implementations (fraud detection, payment gateways, notifications)
  - `/Core/Repositories` - Data access layer implementations
- `/Controllers` - API controllers
- `/Hubs` - SignalR hubs for real-time communication
- `/Infrastructure` - Core infrastructure services and Unit of Work pattern
- `/Middleware` - Custom middleware components (Rate Limiting, Request Logging, Exception Handling)
- `/Models` - Data models and DTOs
- `/Pages` - Razor Pages (Dashboard, Analytics, Settings, Alerts, Transactions)
- `/wwwroot` - Static assets (CSS, JS, images)

### Custom Middlewares (Production-Ready)
1. **ExceptionHandlingMiddleware** - Global error handling with environment-aware diagnostics
2. **RequestLoggingMiddleware** - Request/response logging with unique request IDs and performance tracking
3. **RateLimitingMiddleware** - Thread-safe rate limiting with sliding window (100 req/min per IP/endpoint)

## Features Summary
✅ Real-time fraud detection with ML models
✅ Multi-gateway payment support
✅ Comprehensive admin dashboard
✅ Advanced analytics and reporting
✅ Bulk alert management
✅ CSV/PDF export functionality
✅ Dark mode support
✅ Responsive mobile design
✅ Professional UI/UX
✅ Real-time notifications
✅ Behavioral analytics
✅ Geolocation risk assessment
✅ Configurable alert rules
✅ Webhook integrations

## Future Enhancements
- Geographic map visualization with real coordinates
- Advanced ML model retraining interface
- User role management and permissions
- Multi-language support
- Advanced reporting with custom date ranges
- Integration with more payment gateways
- Enhanced behavioral pattern detection
- Automated response actions based on rules
