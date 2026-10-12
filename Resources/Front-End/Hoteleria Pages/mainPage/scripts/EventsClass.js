import {EVENTO} from "./AcceptedEvents.js"
import {chat_container} from "./tirador-derecho.js"

export class Events{
    constructor(event, element){
        this.#eventHandler(event, element)
    }

    #eventHandler(event, element){
        if(event == EVENTO.redimensionar){
            event = EVENTO.redimensionar
            this.#stretchTrimEvent(event, element)
        }else if(event == EVENTO.toggle_chat){
            event = EVENTO.click
            this.#onCloseChatEvent(event, element)
        }else{
            this.#unknownEvent(event,"UNKNOWN-EVENT", element);
        }
    }

    #onCloseChatEvent(event, element){

        element.addEventListener("click", (e) => {
            
            let current_wdth = parseFloat(window.getComputedStyle(chat_container).width);

            console.log("Se entro al evento para abrir/cerrar chat")

            if(current_wdth == 0){
                console.log("Se abre chat")
                chat_container.style.width = "50%"
            }else{
                console.log("Se cierra chat")
                chat_container.style.width = "0px"
            }

        }
        );
    }

    #stretchTrimEvent(event, element){
        //el "event" solo es el nombre que usamos para la asignacion de implementacion de evento, no es el objeto que guarda la
        //informacion del evento dentro del elemento dado
        console.log(`VALIDATION: El evento -> "${event}" fue reconocido!!!`)

        let current_pointerX_position = 0
        let initial_element_width = 0

        const arrastrar = (e)=>{

            let new_current_pointerX_position = e.clientX
            let width_to_add = 0;
            if(current_pointerX_position > new_current_pointerX_position){
                width_to_add = current_pointerX_position - new_current_pointerX_position 
                chat_container.style.width = `${width_to_add + initial_element_width}px`
            }else if(current_pointerX_position < new_current_pointerX_position){
                width_to_add = new_current_pointerX_position - current_pointerX_position
                chat_container.style.width = `${initial_element_width - width_to_add }px`
            }

            console.log("se sigue moviendo!!")
            
        }

        const soltar = () => {
            console.log("se dejo de presionar el boton de click")
            document.removeEventListener("mousemove", arrastrar);
            document.removeEventListener("mouseup", soltar);
            
        }

        element.addEventListener("mousedown", (e) =>{

            e.preventDefault();

            current_pointerX_position = e.clientX
            initial_element_width = parseFloat(window.getComputedStyle(chat_container).width)


            console.log(`Del tirador: ${current_pointerX_position}`)

            console.log(`Estilos recibidos: current-pos: ${current_pointerX_position} | current-element-width: ${initial_element_width}`)

            document.addEventListener("mousemove", arrastrar)
            document.addEventListener("mouseup", soltar)
        } );

        
    }

    #unknownEvent(real_vent, event, element ){
        console.log(`ERROR: El evento -> "${event} by recibed: ${real_vent}" no se reconoce dentro del sistema!!!`)
    }

}