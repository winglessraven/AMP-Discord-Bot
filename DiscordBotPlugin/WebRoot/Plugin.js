/* jshint undef: true, unused: false */
/* global API,UI,PluginHandler */

this.plugin = {
    PreInit: function () {
        //Called prior to the plugins initialisation, before the tabs are loaded.
        //This method must not invoke any module/plugin specific API calls.
    },

    PostInit: function () {
        //The tabs have been loaded. You should wire up any event handlers here.
        //You can use module/specific API calls here.
    },

    Reset: function () {
        //Tear everything down. Ideally it should be possible to continually do 
        //PreInit -> PostInit -> Reset -> PreInit -> PostInit... over and over.
    },

    StartupFailure: function () {
        //Gets called when the application fails to start. Mainly for Modules rather than plugins.
    },

    SettingChanged: function (node, value) {
        //Invoked whenever a setting is changed. Only applies to changes in the current session rather
        //than made by other users.
    },

    AMPDataLoaded: function () {
        //Any data you might need has been loaded, you should wire up event handlers which use data such as settings here.
    },

    PushedMessage: function (message, data)
    {
        //Invoked when your associated plugin invokes IPluginMessagePusher.Push(message, data) - you only recieve messages pushed
        //from your plugin and not from other plugins/modules.
    }
};

this.tabs = [
    {
        File: "SampleTab.html", //URL to fetch the tab contents from. Relative to the plugin WebRoot directory.
        ExternalTab: false,   //If True, 'File' is treated as an absolute URL to allow contents to be loaded from elsewhere. 
        //Note that the appropriate CORS headers are required on the hosting server to allow this.
        ShortName: "Sample",    //Name used for the element. Prefixed with tab_PLUGINNAME_
        Name: "Sample",     //Display name for the tab.
        Icon: "",             //Icon to show in the tab.
        Light: false,                //Use the 'light' theme for this tab.
        Category: ""
    }
];

this.stylesheet = "";    //Styles for tab-specific styles

//Put your code here.